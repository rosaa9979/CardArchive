using System;
using System.Collections.Generic;
using System.Linq;

namespace TcgEngine.Gameplay
{
    public partial class GameLogic
    {
        private IEnumerable<Card> GetOngoingSources(Player player)
        {
            if (player.hero != null) yield return player.hero;
            for (int i = 0; i < player.cards_board.Count; i++) yield return player.cards_board[i];
            for (int i = 0; i < player.cards_equip.Count; i++) yield return player.cards_equip[i];
            for (int i = 0; i < player.cards_attach.Count; i++) yield return player.cards_attach[i];
            for (int i = 0; i < player.cards_club.Count; i++) yield return player.cards_club[i];
            for (int i = 0; i < player.player_ability.Count; i++) yield return player.player_ability[i];
        }

        // Calculation only: no Effect dispatch, event registration, or persistent runtime rule list.
        public NumericValue ResolveNumericOperation(NumericOperation request)
        {
            if (request.kind != NumericKind.Damage && request.kind != NumericKind.Heal)
                throw new ArgumentException("A health request requires Damage or Heal.");
            if (!(request.target is Card) && !(request.target is Player))
                throw new ArgumentException("A health request requires a Card or Player recipient.");

            var rules = new List<(NumericModifierData data, Lazy<bool> applies)>();
            // Stable order shared by Live and cloned AI games, including equal/unassigned play orders.
            var sources = game_data.players.SelectMany(GetOngoingSources).Where(c => c != null)
                .Distinct().OrderBy(c => c.play_order).ThenBy(c => c.player_id).ThenBy(c => c.uid, StringComparer.Ordinal);
            foreach (Card caster in sources)
            {
                if (!caster.CanDoAbilities()) continue;
                var abilities = new HashSet<string>();
                foreach (AbilityData ability in caster.GetAbilities())
                {
                    if (ability == null || ability.trigger != AbilityTrigger.Ongoing || !abilities.Add(ability.id)
                        || ability.data_containers == null) continue;
                    var containers = ability.data_containers.OfType<NumericModifierData>().Distinct()
                        .Where(c => c.cause == NumericCause.Any || c.cause == request.cause).ToList();
                    if (containers.Count == 0) continue;
                    // Eligibility belongs to this request and source ability, not each container.
                    // Defer actual predicates until a rule matches the kind at its execution stage.
                    var applies = new Lazy<bool>(() =>
                    {
                        var context = new AbilityExecutionContext(game_data, caster, null, null);
                        var query = AbilityExecutionContext.Query(context);
                        if (!GetOngoingTargets(ability, caster, query).Any(t => ReferenceEquals(t, request.target)))
                            return false;
                        return ability.AreTriggerConditionsMet(game_data, caster, context)
                            && GetOngoingTargets(ability, caster, context).Any(t => ReferenceEquals(t, request.target));
                    });
                    foreach (NumericModifierData container in containers)
                    {
                        ValidateNumericModifier(container);
                        rules.Add((container, applies));
                    }
                }
            }

            NumericKind kind = request.kind;
            int value = Math.Max(0, request.value);
            // Each rule is visited once in its stage. Conversion never calls a public damage/heal API.
            foreach (var entry in rules)
            {
                NumericModifierData rule = entry.data;
                if (rule.operation == NumericOperator.ConvertKind && MatchesNumericKind(rule, kind) && entry.applies.Value)
                    kind = rule.output_kind;
            }
            foreach (var entry in rules)
            {
                NumericModifierData rule = entry.data;
                if (rule.operation == NumericOperator.ConvertKind || !MatchesNumericKind(rule, kind) || !entry.applies.Value) continue;
                long next = value;
                switch (rule.operation)
                {
                    case NumericOperator.Set: next = rule.value; break;
                    case NumericOperator.Add: next = (long)value + rule.value; break;
                    case NumericOperator.Multiply: next = (long)value * rule.value / rule.divisor; break;
                }
                value = (int)Math.Max(0L, Math.Min(int.MaxValue, next));
            }
            return new NumericValue(kind, value);
        }

        private static bool MatchesNumericKind(NumericModifierData rule, NumericKind kind)
        {
            return rule.input_kind == NumericKind.Any || rule.input_kind == kind;
        }

        // The editor validator uses the same contract as the interpreter.
        public static void ValidateNumericModifier(NumericModifierData rule)
        {
            if (rule == null) throw new ArgumentException("Missing numeric container.");
            if (!Enum.IsDefined(typeof(NumericKind), rule.input_kind)
                || !Enum.IsDefined(typeof(NumericCause), rule.cause)
                || !Enum.IsDefined(typeof(NumericOperator), rule.operation))
                throw new ArgumentException("Unknown numeric kind, cause or operator.");
            if (rule.operation == NumericOperator.ConvertKind
                && rule.output_kind != NumericKind.Damage && rule.output_kind != NumericKind.Heal)
                throw new ArgumentException("A conversion requires Damage or Heal as output.");
            if (rule.operation == NumericOperator.Set && rule.value < 0)
                throw new ArgumentException("Set requires a nonnegative health amount.");
            if (rule.operation == NumericOperator.Multiply && (rule.value < 0 || rule.divisor <= 0))
                throw new ArgumentException("Multiply requires a nonnegative numerator and positive divisor.");
        }

        private bool AcceptsNumericOperation(NumericOperation request, NumericKind kind)
        {
            if (request.target == null) return false;
            if (request.target is Card card)
            {
                if (card.HasStatus(StatusType.Invincibility)) return false;
                if (kind == NumericKind.Damage && card.HasStatus(StatusType.SpellImmunity)
                    && (request.source == null || !request.source.CardData.IsCitizen())) return false;
            }
            return true;
        }

        private void ApplyNumericOperation(NumericOperation request, bool spellDamage = false)
        {
            // Preserve existing admission checks; a converted operation must also be legal.
            if (!AcceptsNumericOperation(request, request.kind)) return;
            NumericValue adjusted = ResolveNumericOperation(request);
            if (!AcceptsNumericOperation(request, adjusted.kind)) return;
            if (request.target is Card card)
            {
                if (adjusted.kind == NumericKind.Heal) ApplyCardHealing(request.source, card, adjusted.value);
                else ApplyCardDamage(request, card, adjusted.value, spellDamage);
            }
            else if (request.target is Player player)
            {
                ApplyPlayerHealth(request, player, adjusted);
            }
        }

        private void ApplyCardHealing(Card source, Card target, int value)
        {
            int before = target.damage;
            target.damage = (int)Math.Max(0L, (long)target.damage - value);
            if (!target.dying && target.GetHP() > 0)
            {
                target.death_source_uid = null;
                target.death_source_counter = false;
                target.death_cause = DeathCause.Unknown;
            }
            game_data.GetPlayer(target.player_id).total_heal += before - target.damage;
            onCardHealed?.Invoke(source, target, before - target.damage);
            // A zero amount is still a healing operation, not cancellation.
            TriggerCardAbilityType(AbilityTrigger.OnHeal, target);
            TriggerOtherCardsAbilityType(AbilityTrigger.OnHealOther, target);
        }

        private void ApplyCardDamage(NumericOperation request, Card target, int value, bool spellDamage)
        {
            Card attacker = request.source;
            bool sourcedDamage = attacker != null;
            if (sourcedDamage)
            {
                if (value > 0 && target.HasStatus(StatusType.Shell))
                {
                    target.RemoveStatus(StatusType.Shell);
                    return;
                }
                if (!spellDamage) value = Math.Max(0, value - target.GetStatusValue(StatusType.Armor));
            }
            int damageMax = Math.Min(value, Math.Max(0, target.GetHP()));
            int extra = value - target.GetHP();
            target.damage = (int)Math.Min(int.MaxValue, (long)target.damage + value);
            onCardDamaged?.Invoke(attacker, target, value);

            if (sourcedDamage)
            {
                bool counter = request.cause == NumericCause.CounterAttack;
                if (value > 0 && target.GetHP() <= 0)
                    SetDeathSource(attacker, target, counter, counter ? DeathCause.CounterAttackDamage
                        : (request.cause == NumericCause.Ability || spellDamage ? DeathCause.AbilityDamage : DeathCause.AttackDamage));
                if (value > 0) TriggerCardAbilityType(AbilityTrigger.OnAfterDamage, attacker, target);
                if (!spellDamage && extra > 0 && attacker.player_id == game_data.current_player && attacker.HasStatus(StatusType.Trample))
                    ApplyNumericOperation(new NumericOperation(NumericKind.Damage, NumericCause.Trample, extra, attacker, game_data.GetPlayer(target.player_id)));
                if (!spellDamage && attacker.HasStatus(StatusType.LifeSteal))
                    ApplyNumericOperation(new NumericOperation(NumericKind.Heal, NumericCause.LifeSteal, damageMax, attacker, game_data.GetPlayer(attacker.player_id)));
                target.RemoveStatus(StatusType.Sleep);
                if (request.cause == NumericCause.Ability && value > 0 && attacker.HasStatus(StatusType.Deathtouch) && target.CardData.IsCitizen())
                    KillCard(attacker, target);
            }
            else if (target.GetHP() <= 0 && !game_data.IsOnBoard(target))
                DiscardCard(target);
        }

        private void ApplyPlayerHealth(NumericOperation request, Player target, NumericValue adjusted)
        {
            int before = target.hp;
            long next = adjusted.kind == NumericKind.Heal ? (long)before + adjusted.value : (long)before - adjusted.value;
            // Preserve the existing lifesteal/poison/trample rules instead of silently changing their caps/events.
            bool bounded = request.cause != NumericCause.LifeSteal && request.cause != NumericCause.Status && request.cause != NumericCause.Trample;
            target.hp = (int)Math.Max(bounded ? 0L : int.MinValue, Math.Min(bounded ? (long)target.hp_max : int.MaxValue, next));
            if (adjusted.kind == NumericKind.Heal)
            {
                if (request.cause != NumericCause.LifeSteal) target.total_heal += target.hp - before;
                onPlayerHealed?.Invoke(request.source, target, target.hp - before);
                return;
            }
            // Fatigue keeps its own notification; every other cause reports the applied damage.
            if (request.cause == NumericCause.Fatigue) onExhaustDamage?.Invoke(target);
            else onPlayerDamaged?.Invoke(request.source, target, adjusted.value);

            if ((request.cause == NumericCause.Attack || request.cause == NumericCause.Ability)
                && request.source != null && request.source.HasStatus(StatusType.LifeSteal))
                ApplyNumericOperation(new NumericOperation(NumericKind.Heal, NumericCause.LifeSteal, adjusted.value, request.source, game_data.GetPlayer(request.source.player_id)));
        }
    }
}
