#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TcgEngine.Gameplay;
using TcgEngine.Replay;
using UnityEditor;
using UnityEngine;

namespace TcgEngine.EditorValidation
{
    public static class NumericContainerValidation
    {
        [Serializable] private class Result { public string scenario, mode, error; public bool passed; }
        [Serializable] private class Report { public List<Result> results = new List<Result>(); }
        private static readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();
        private static readonly List<string> abilityIds = new List<string>();
        private static Report report;
        private static bool prediction;

        private static T Make<T>() where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>(); temporary.Add(obj); return obj;
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static Slot S(int x, int y) { return Slot.GetAll().First(s => s.x == x && s.y == y); }
        private static void Case(string name, Action body)
        {
            var result = new Result { scenario = name, mode = prediction ? "AI" : "Live" };
            try { body(); result.passed = true; } catch (Exception error) { result.error = error.ToString(); }
            report.results.Add(result);
        }

        private sealed class Fixture
        {
            public readonly Game game = new Game("pipeline-validation", 2) { state = GameState.Play, phase = GamePhase.Main };
            public readonly GameLogic logic;
            public Fixture()
            {
                logic = prediction ? new GameLogic(true) : new GameLogic(game);
                if (prediction) logic.SetData(game);
                logic.SetImmediateResolution(true);
                game.SetRandom(new GameRandom(6));
                foreach (Player player in game.players) { player.hp = player.hp_max = 100; player.mana = player.mana_max = 100; }
                Add("Peroro_Tlush", 0, Slot.None);
                Add("Peroro_Tlush", 1, Slot.None);
            }
            public Card Add(string id, int owner, Slot slot)
            {
                var player = game.GetPlayer(owner);
                Card card = Card.Create(CardData.Get(id), VariantData.GetDefault(), player);
                if (slot.IsValid()) { card.slot = slot; player.cards_board.Add(card); logic.AssignPlayOrder(card); }
                else player.cards_hand.Add(card);
                return card;
            }
            public void Settle()
            {
                for (int i = 0; i < 1000 && logic.IsResolving() && game.selector == SelectorType.None; i++) logic.Update(1);
                Check(game.selector != SelectorType.None || !logic.IsResolving(), "queue did not settle");
            }
            public void Roundtrip()
            {
                var snapshot = ReplayStateCodec.Capture(game);
                Check(ReplayStateCodec.Hash(snapshot) == ReplayStateCodec.Hash(ReplayStateCodec.Capture(ReplayStateCodec.Restore(snapshot))), "snapshot roundtrip");
            }
        }

        private static AbilityData Ability(AbilityTarget target)
        {
            var ability = Make<AbilityData>(); ability.id = "numeric-test-" + abilityIds.Count;
            ability.criteria_target = target;
            ability.conditions_trigger = ability.conditions_criteria_target = ability.condition_target = Array.Empty<ConditionData>();
            ability.filters_target = Array.Empty<FilterData>(); ability.effects = Array.Empty<EffectData>();
            ability.status = Array.Empty<StatusData>(); ability.chain_abilities = Array.Empty<AbilityData>();
            var range = Make<ConditionWideAreaRange>(); range.directions = new List<Direction> { new Direction() };
            ability.condition_wide_range = range;
            AbilityData.ability_dict.Add(ability.id, ability); abilityIds.Add(ability.id);
            return ability;
        }

        [MenuItem("TcgEngine/Validate Numeric Containers")]
        public static void Run()
        {
            CardData.Load(); AbilityData.Load(); VariantData.Load(); TraitData.Load(); MarkerData.Load(); StatusData.Load(); WeaponData.Load(); ClubData.Load();
            var instanceField = typeof(DataLoader).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            object previousLoader = instanceField.GetValue(null);
            var host = new GameObject("Ability pipeline validation");
            var loader = host.AddComponent<DataLoader>(); loader.data = Resources.LoadAll<GameplayData>("").First(); instanceField.SetValue(null, loader);
            bool logging = Debug.unityLogger.logEnabled; Debug.unityLogger.logEnabled = false;
            report = new Report();
            try
            {
                foreach (bool mode in new[] { false, true }) { prediction = mode; Scenarios(); }
            }
            finally
            {
                instanceField.SetValue(null, previousLoader); UnityEngine.Object.DestroyImmediate(host);
                foreach (string id in abilityIds) AbilityData.ability_dict.Remove(id); abilityIds.Clear();
                foreach (var obj in temporary) UnityEngine.Object.DestroyImmediate(obj); temporary.Clear();
                Debug.unityLogger.logEnabled = logging;
            }
            string directory = Path.Combine(Application.dataPath, "../.ai-work/numeric-containers");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "numeric-tests.json"), JsonUtility.ToJson(report, true));
            int failed = report.results.Count(r => !r.passed);
            Debug.Log("NUMERIC_CONTAINERS passed=" + (report.results.Count - failed) + " failed=" + failed);
            if (failed > 0) throw new Exception(string.Join("\n", report.results.Where(r => !r.passed).Select(r => r.mode + " " + r.scenario + ": " + r.error)));
        }

        private static NumericModifierData Rule(NumericKind kind, NumericOperator operation, int value = 0,
            NumericCause cause = NumericCause.Any, NumericKind output = NumericKind.Heal, int divisor = 1)
        {
            var rule = Make<NumericModifierData>(); rule.input_kind = kind; rule.operation = operation;
            rule.value = value; rule.cause = cause; rule.output_kind = output; rule.divisor = divisor; return rule;
        }
        private static AbilityData Bind(Card card, AbilityTarget target, params NumericModifierData[] rules)
        {
            var a = Ability(target); a.trigger = AbilityTrigger.Ongoing; a.data_containers = rules;
            card.AddAbility(a); return a;
        }
        private static NumericValue Calculate(Fixture f, Card target, NumericKind kind, int amount, NumericCause cause = NumericCause.Ability)
        {
            return f.logic.ResolveNumericOperation(new NumericOperation(kind, cause, amount, null, target));
        }
        private static void Scenarios()
        {
            Case("Irrelevant kinds causes and recipients do not consume probability", () => {
                foreach (int mismatch in new[] { 0, 1, 2 }) {
                    var f = new Fixture(); var source = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                    var target = mismatch == 2 ? f.Add("Commuting_Student_MIDDLE", 1, S(3, 2)) : source;
                    var a = Bind(source, AbilityTarget.Self, Rule(mismatch == 0 ? NumericKind.Heal : NumericKind.Damage,
                        NumericOperator.Set, 0, mismatch == 1 ? NumericCause.Attack : NumericCause.Any));
                    var chance = Make<ConditionPossibility>(); chance.possibility = .5f;
                    a.conditions_trigger = new ConditionData[] { chance };
                    var expected = ((GameRandom)f.game.GetRandom()).Clone();
                    Check(Calculate(f, target, NumericKind.Damage, 3).value == 3, "irrelevant rule applied");
                    Check(expected.NextDouble() == f.game.GetRandom().NextDouble(), "irrelevant rule consumed a roll: " + mismatch);
                }
            });
            Case("Conversion and final-kind rules share one applicability decision", () => {
                foreach (ConditionEvaluationPolicy policy in Enum.GetValues(typeof(ConditionEvaluationPolicy))) {
                    var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                    var a = Bind(c, AbilityTarget.Self, Rule(NumericKind.Heal, NumericOperator.Set, 7),
                        Rule(NumericKind.Damage, NumericOperator.ConvertKind), Rule(NumericKind.Heal, NumericOperator.Add, 2));
                    var chance = Make<ConditionPossibility>(); chance.possibility = .5f; chance.evaluation = policy;
                    a.conditions_trigger = new ConditionData[] { chance };
                    var expected = ((GameRandom)f.game.GetRandom()).Clone();
                    for (int i = 0; i < 256; i++) {
                        bool pass = expected.NextDouble() < .5;
                        var result = Calculate(f, c, NumericKind.Damage, 3);
                        Check(result.kind == (pass ? NumericKind.Heal : NumericKind.Damage)
                            && result.value == (pass ? 9 : 3), "eligibility was rerolled between rules or stages");
                    }
                    Check(expected.NextDouble() == f.game.GetRandom().NextDouble(), "unexpected condition draw count");
                }
            });
            Case("Final-kind matching activates previously unmatched rules without premature rolls", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var heal = Bind(c, AbilityTarget.Self, Rule(NumericKind.Heal, NumericOperator.Set, 7));
                var chance = Make<ConditionPossibility>(); chance.possibility = .5f; chance.evaluation = ConditionEvaluationPolicy.EvaluateOnce;
                heal.conditions_trigger = new ConditionData[] { chance };
                Bind(c, AbilityTarget.Self, Rule(NumericKind.Damage, NumericOperator.ConvertKind));
                var expected = ((GameRandom)f.game.GetRandom()).Clone();
                for (int i = 0; i < 256; i++) {
                    bool pass = expected.NextDouble() < .5; var result = Calculate(f, c, NumericKind.Damage, 3);
                    Check(result.kind == NumericKind.Heal && result.value == (pass ? 7 : 3), "final-kind rule was discarded or rerolled");
                }
                Check(expected.NextDouble() == f.game.GetRandom().NextDouble(), "final-kind draw count");
            });
            Case("Separate abilities sharing a condition still roll independently", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var chance = Make<ConditionPossibility>(); chance.possibility = .5f; chance.evaluation = ConditionEvaluationPolicy.EvaluateOnce;
                var one = Bind(c, AbilityTarget.Self, Rule(NumericKind.Damage, NumericOperator.Add, 1));
                var two = Bind(c, AbilityTarget.Self, Rule(NumericKind.Damage, NumericOperator.Add, 2));
                one.conditions_trigger = two.conditions_trigger = new ConditionData[] { chance };
                var expected = ((GameRandom)f.game.GetRandom()).Clone();
                for (int i = 0; i < 256; i++) {
                    int value = 3 + (expected.NextDouble() < .5 ? 1 : 0) + (expected.NextDouble() < .5 ? 2 : 0);
                    Check(Calculate(f, c, NumericKind.Damage, 3).value == value, "shared asset shared a request decision");
                }
                Check(expected.NextDouble() == f.game.GetRandom().NextDouble(), "independent abilities draw count");
            });
            Case("Numeric recipient probability queries do not reroll actual criteria", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var a = Bind(c, AbilityTarget.Self, Rule(NumericKind.Damage, NumericOperator.Set, 0), Rule(NumericKind.Damage, NumericOperator.Add, 1));
                var chance = Make<ConditionPossibility>(); chance.possibility = .5f; chance.evaluation = ConditionEvaluationPolicy.EvaluateOnce;
                a.conditions_criteria_target = new ConditionData[] { chance };
                var expected = ((GameRandom)f.game.GetRandom()).Clone();
                for (int i = 0; i < 256; i++) {
                    bool pass = expected.NextDouble() < .5;
                    Check(Calculate(f, c, NumericKind.Damage, 3).value == (pass ? 1 : 3), "query consumed random or actual criteria rerolled");
                }
                Check(expected.NextDouble() == f.game.GetRandom().NextDouble(), "criteria draw count");
            });
            Case("Ratio effect uses ability cause on both cards and players", () => {
                var f = new Fixture(); var caster = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var target = f.Add("Commuting_Student_MIDDLE", 1, S(3, 2)); target.hp = 10;
                var rule = Rule(NumericKind.Damage, NumericOperator.Set, 0, NumericCause.Ability);
                var cardAbility = Bind(target, AbilityTarget.Self, rule);
                var playerAbility = Bind(target, AbilityTarget.PlayerSelf, rule);
                var ratio = Make<EffectDamageRatio>(); ratio.ratio = .5f; var effectAbility = Ability(AbilityTarget.None);
                ratio.DoEffect(f.logic, effectAbility, caster, target);
                ratio.DoEffect(f.logic, effectAbility, caster, f.game.players[1]);
                Check(target.damage == 0 && f.game.players[1].hp == 100, "ratio bypassed ability modifiers");
                rule.cause = NumericCause.Attack;
                ratio.DoEffect(f.logic, effectAbility, caster, target);
                ratio.DoEffect(f.logic, effectAbility, caster, f.game.players[1]);
                Check(target.damage == 5 && f.game.players[1].hp == 50, "ratio matched attack-only modifier");
                f.logic.DamageCard(caster, target, 1); f.logic.DamagePlayer(caster, f.game.players[1], 1);
                Check(target.damage == 5 && f.game.players[1].hp == 50, "ordinary attack cause changed");
            });

            Case("Container-only abilities evaluate conditions only for numeric requests", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var a = Bind(c, AbilityTarget.Self, Rule(NumericKind.Damage, NumericOperator.Add, 1));
                var condition = Make<PipelineProbeCondition>(); condition.evaluation = ConditionEvaluationPolicy.EvaluateOnce;
                a.conditions_criteria_target = new ConditionData[] { condition };
                f.logic.UpdateOngoing(); f.logic.UpdateOngoing();
                Check(condition.calls == 0, "container condition ran during ongoing effect recalculation");
                Check(Calculate(f, c, NumericKind.Damage, 1).value == 2 && condition.calls == 2, "expected one recipient query and one actual condition evaluation");
            });
            Case("Overlapping aura centers contribute one rule to a recipient", () => {
                var f = new Fixture(); var source = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var target = f.Add("Commuting_Student_MIDDLE", 1, S(3, 2));
                var a = Bind(source, AbilityTarget.AllCardsBoard, Rule(NumericKind.Damage, NumericOperator.Add, 1));
                a.condition_wide_range.directions = new List<Direction> { new Direction(), new Direction { dx = 1, dy = 0 }, new Direction { dx = -1, dy = 0 } };
                Check(Calculate(f, target, NumericKind.Damage, 1).value == 2, "overlapping range doubled modifier");
            });
            Case("Trample damage passes the target player's modifiers", () => {
                var f = new Fixture(); var attacker = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var target = f.Add("Commuting_Student_MIDDLE", 1, S(3, 2));
                attacker.AddStatus(StatusType.Trample, 1, 0);
                Bind(target, AbilityTarget.PlayerSelf, Rule(NumericKind.Damage, NumericOperator.Set, 0, NumericCause.Trample));
                f.logic.DamageCard(attacker, target, 4);
                Check(target.damage == 4 && f.game.players[1].hp == 100, "trample bypassed modifier");
            });
            Case("Condition results are not shared between separate numeric requests", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var a = Bind(c, AbilityTarget.Self, Rule(NumericKind.Damage, NumericOperator.Set, 0));
                var condition = Make<PipelineProbeCondition>(); condition.evaluation = ConditionEvaluationPolicy.EvaluateOnce;
                condition.triggerResult = false; a.conditions_trigger = new ConditionData[] { condition };
                Check(Calculate(f, c, NumericKind.Damage, 3).value == 3, "false trigger applied rule");
                condition.triggerResult = true; Check(Calculate(f, c, NumericKind.Damage, 3).value == 0, "previous result leaked");
            });
            Case("Converted healing clears pending lethal attribution before the death step", () => {
                var f = new Fixture(); var c = f.Add("Shishido_Izumi", 0, S(2, 2));
                var attacker = f.Add("Commuting_Student_MIDDLE", 1, S(3, 2));
                f.logic.DamageCard(attacker, c, c.GetHP()); Check(c.GetHP() == 0 && c.death_source_uid == attacker.uid, "missing pending lethal damage");
                f.logic.DamageCard_Event(attacker, c, 1, true);
                Check(c.GetHP() == 1 && c.death_source_uid == null && !c.dying, "converted heal did not save pending death");
            });
            Case("Numeric interpretation runs on an AI worker using cloned state", () => {
                var f = new Fixture(); var c = f.Add("Shishido_Izumi", 0, S(2, 2));
                var clone = Game.CloneNew(f.game); var logic = new GameLogic(true); logic.SetData(clone);
                var request = new NumericOperation(NumericKind.Damage, NumericCause.Ability, 3, null, clone.GetCard(c.uid));
                var value = System.Threading.Tasks.Task.Run(() => logic.ResolveNumericOperation(request)).GetAwaiter().GetResult();
                Check(value.kind == NumericKind.Heal && value.value == 3, "worker prediction result");
            });
            Case("Junko data prevents healing but still emits OnHeal", () => {
                var f = new Fixture(); var c = f.Add("Akashi_Junko", 0, S(2, 2)); c.damage = 2;
                var listener = Ability(AbilityTarget.Self); listener.trigger = AbilityTrigger.OnHeal; c.AddAbility(listener);
                int events = 0; f.logic.onAbilityStart += (a, _) => { if (a.id == listener.id) events++; };
                f.logic.HealCard(c, 2); f.Settle();
                Check(c.damage == 2 && f.game.players[0].total_heal == 0 && events == 1, "zero healing lost event or changed health");
            });
            Case("Junko silence disables the data modifier", () => {
                var f = new Fixture(); var c = f.Add("Akashi_Junko", 0, S(2, 2)); c.damage = 2;
                c.AddStatus(StatusType.Silenced, 1, 0); f.logic.HealCard(c, 1);
                Check(c.damage == 1, "silenced Junko still suppresses healing");
                c.RemoveStatus(StatusType.Silenced); f.logic.HealCard(c, 1); Check(c.damage == 1, "modifier did not resume");
            });
            Case("Izumi converts ability damage before shell without damage events", () => {
                var f = new Fixture(); var c = f.Add("Shishido_Izumi", 0, S(2, 2)); c.damage = 3;
                var attacker = f.Add("Commuting_Student_MIDDLE", 1, S(3, 2));
                c.AddStatus(StatusType.Shell, 1, 0); int damageEvents = 0; f.logic.onCardDamaged += (_, __, ___) => damageEvents++;
                f.logic.DamageCard_Event(attacker, c, 2, true); f.Settle();
                Check(c.damage == 1 && damageEvents == 0 && c.HasStatus(StatusType.Shell), "conversion emitted damage or consumed shell");
            });
            Case("Izumi does not convert attacks counterattacks or poison", () => {
                var f = new Fixture(); var c = f.Add("Shishido_Izumi", 0, S(2, 2)); c.hp = 20;
                var attacker = f.Add("Commuting_Student_MIDDLE", 1, S(3, 2));
                f.logic.DamageCard(attacker, c, 1, true); f.logic.DamageCard(attacker, c, 1, false, true); f.logic.DamageCard_Event(c, 1);
                Check(c.damage == 3, "non-ability damage converted");
            });
            Case("Izumi silence and ability removal disable conversion", () => {
                var f = new Fixture(); var c = f.Add("Shishido_Izumi", 0, S(2, 2));
                var attacker = f.Add("Commuting_Student_MIDDLE", 1, S(3, 2));
                c.AddStatus(StatusType.Silenced, 1, 0); f.logic.DamageCard_Event(attacker, c, 1, true);
                Check(c.damage == 1, "silenced conversion"); c.RemoveStatus(StatusType.Silenced);
                var a = c.GetAbilities().Single(x => x.id == "OG_ability_damage_to_heal"); c.RemoveAbility(a);
                f.logic.DamageCard_Event(attacker, c, 1, true); Check(c.damage == 2, "removed conversion");
            });
            Case("Converted healing receives final-kind value rules regardless of array position", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); c.hp = 10; c.damage = 2;
                Bind(c, AbilityTarget.Self, Rule(NumericKind.Heal, NumericOperator.Set, 0), Rule(NumericKind.Damage, NumericOperator.ConvertKind));
                var attacker = f.Add("Commuting_Student_MIDDLE", 1, S(3, 2));
                f.logic.DamageCard_Event(attacker, c, 3, true); Check(c.damage == 2, "healing rule evaluated before conversion");
            });
            Case("No containers preserve kind and amount", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                foreach (NumericKind kind in new[] { NumericKind.Damage, NumericKind.Heal }) {
                    var value = Calculate(f, c, kind, 7); Check(value.kind == kind && value.value == 7, "identity failed");
                }
            });
            Case("Set add and rational multiply obey declared array order", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                Bind(c, AbilityTarget.Self, Rule(NumericKind.Damage, NumericOperator.Set, 7), Rule(NumericKind.Damage, NumericOperator.Add, 2),
                    Rule(NumericKind.Damage, NumericOperator.Multiply, 1, divisor: 2));
                Check(Calculate(f, c, NumericKind.Damage, 3).value == 4, "expected floor((7+2)/2)");
            });
            Case("Cause filters ignore unrelated requests", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                Bind(c, AbilityTarget.Self, Rule(NumericKind.Damage, NumericOperator.Set, 0, NumericCause.Attack));
                Check(Calculate(f, c, NumericKind.Damage, 3, NumericCause.Attack).value == 0, "attack not matched");
                Check(Calculate(f, c, NumericKind.Damage, 3).value == 3, "ability matched attack-only rule");
            });
            Case("Conversion cycles visit each declaration once and terminate", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                Bind(c, AbilityTarget.Self, Rule(NumericKind.Damage, NumericOperator.ConvertKind),
                    Rule(NumericKind.Heal, NumericOperator.ConvertKind, output: NumericKind.Damage));
                var value = Calculate(f, c, NumericKind.Damage, 3); Check(value.kind == NumericKind.Damage && value.value == 3, "conversion cycle");
            });
            Case("Duplicate references and duplicate ability IDs do not multiply a rule", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var r = Rule(NumericKind.Damage, NumericOperator.Add, 1);
                var a = Bind(c, AbilityTarget.Self, r, r); c.AddAbility(a);
                Check(Calculate(f, c, NumericKind.Damage, 1).value == 2, "duplicate declaration counted twice");
            });
            Case("Repeated operations remain independent and assets remain unchanged", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var r = Rule(NumericKind.Damage, NumericOperator.Add, 2);
                Bind(c, AbilityTarget.Self, r); var request = new NumericOperation(NumericKind.Damage, NumericCause.Ability, 1, null, c);
                Check(f.logic.ResolveNumericOperation(request).value == 3 && f.logic.ResolveNumericOperation(request).value == 3
                    && request.value == 1 && r.value == 2, "mutable asset or leaked operation");
            });
            Case("Auras use shared recipient selection and end when the source leaves", () => {
                var f = new Fixture(); var source = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var target = f.Add("Commuting_Student_MIDDLE", 1, S(3, 2));
                Bind(source, AbilityTarget.AllCardsBoard, Rule(NumericKind.Damage, NumericOperator.Add, 2));
                Check(Calculate(f, target, NumericKind.Damage, 1).value == 3, "aura missing");
                source.AddStatus(StatusType.Silenced, 1, 0); Check(Calculate(f, target, NumericKind.Damage, 1).value == 1, "silenced aura");
                source.RemoveStatus(StatusType.Silenced); f.game.players[0].cards_board.Remove(source); f.game.players[0].cards_discard.Add(source);
                Check(Calculate(f, target, NumericKind.Damage, 1).value == 1, "removed source retained rule");
            });
            Case("Multiple sources use play order independent of board list order", () => {
                var f = new Fixture(); var first = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var second = f.Add("Commuting_Student_MIDDLE", 0, S(3, 2));
                Bind(first, AbilityTarget.AllCardsBoard, Rule(NumericKind.Damage, NumericOperator.Set, 2));
                Bind(second, AbilityTarget.AllCardsBoard, Rule(NumericKind.Damage, NumericOperator.Multiply, 3));
                f.game.players[0].cards_board.Reverse(); Check(Calculate(f, first, NumericKind.Damage, 1).value == 6, "incidental collection order");
            });
            Case("Player targets share the same conversion and healing pipeline", () => {
                var f = new Fixture(); var source = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var p = f.game.players[0]; p.hp = 90;
                Bind(source, AbilityTarget.PlayerSelf, Rule(NumericKind.Damage, NumericOperator.ConvertKind));
                int damageEvents = 0; f.logic.onPlayerDamaged += (_, __, ___) => damageEvents++;
                f.logic.DamagePlayer_Event(source, p, 3); Check(p.hp == 93 && p.total_heal == 3 && damageEvents == 0, "player conversion");
            });
            Case("Fatigue goes through modifiers and retains its notification", () => {
                var f = new Fixture(); var source = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var p = f.game.players[0];
                Bind(source, AbilityTarget.PlayerSelf, Rule(NumericKind.Damage, NumericOperator.Set, 0, NumericCause.Fatigue));
                int events = 0; f.logic.onExhaustDamage += _ => events++; f.logic.DamagePlayer_Exhaust(p);
                Check(p.hp == 100 && p.exhaust_damage == 1 && events == 1, "fatigue bypass or wrong notification");
            });
            Case("Lifesteal gets its own modifier pass without recursive drain", () => {
                var f = new Fixture(); var source = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var p = f.game.players[0];
                source.AddStatus(StatusType.LifeSteal, 1, 0);
                Bind(source, AbilityTarget.PlayerSelf, Rule(NumericKind.Heal, NumericOperator.Set, 2, NumericCause.LifeSteal));
                f.logic.DamagePlayer(source, f.game.players[1], 5);
                Check(p.hp == 102 && p.total_heal == 0 && f.game.players[1].hp == 95, "lifesteal bypass or changed legacy cap");
            });
            Case("Generic and status damage both use numeric correction", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); c.hp = 20;
                Bind(c, AbilityTarget.Self, Rule(NumericKind.Damage, NumericOperator.Set, 2));
                f.logic.DamageCard(c, 5); f.logic.DamageCard_Event(c, 5); Check(c.damage == 4, "generic/status entrypoint bypass");
            });
            Case("Invalid numeric definitions fail before mutating recipient health", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var a = Bind(c, AbilityTarget.Self, Rule(NumericKind.Heal, NumericOperator.Multiply, 2, divisor: 0));
                Check(AbilitySchemaValidation.Inspect(a).Any(), "schema accepted zero divisor");
                bool rejected = false; try { f.logic.HealCard(c, 2); } catch (ArgumentException) { rejected = true; }
                Check(rejected && c.damage == 0, "bad data changed health");
            });
            Case("Numeric arithmetic clamps safely without integer overflow", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var r = Rule(NumericKind.Damage, NumericOperator.Multiply, int.MaxValue); Bind(c, AbilityTarget.Self, r);
                Check(Calculate(f, c, NumericKind.Damage, int.MaxValue).value == int.MaxValue, "positive overflow");
                r.operation = NumericOperator.Add; r.value = int.MinValue;
                Check(Calculate(f, c, NumericKind.Damage, 1).value == 0, "negative amount");
            });
            Case("Cloned and replay-restored games resolve the same data without shared mutable state", () => {
                var f = new Fixture(); var c = f.Add("Shishido_Izumi", 0, S(2, 2)); c.damage = 2;
                var attacker = f.Add("Commuting_Student_MIDDLE", 1, S(3, 2));
                var clone = Game.CloneNew(f.game); var replay = ReplayStateCodec.Restore(ReplayStateCodec.Capture(f.game));
                foreach (Game game in new[] { clone, replay }) {
                    var logic = new GameLogic(true); logic.SetData(game); logic.SetImmediateResolution(true);
                    logic.DamageCard_Event(game.GetCard(attacker.uid), game.GetCard(c.uid), 1, true);
                    Check(game.GetCard(c.uid).damage == 1, "clone/replay lost container");
                }
                Check(c.damage == 2, "clone changed original");
            });
            Case("Containers do not invoke attached Effects or queue a new ability", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); c.hp = 10;
                var a = Bind(c, AbilityTarget.Self, Rule(NumericKind.Damage, NumericOperator.Set, 2));
                a.effects = new EffectData[] { Make<EffectDamage>() }; a.value = 5;
                int starts = 0; f.logic.onAbilityStart += (_, __) => starts++;
                f.logic.DamageCard(c, 3); Check(c.damage == 2 && starts == 0, "container executed an effect/ability");
            });
        }
    }
}
#endif
