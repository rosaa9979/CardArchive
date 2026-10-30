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
    public static class AbilityPipelineValidation
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
            var ability = Make<AbilityData>(); ability.id = "pipeline-test-" + abilityIds.Count;
            ability.criteria_target = target;
            ability.conditions_trigger = ability.conditions_criteria_target = ability.condition_target = Array.Empty<ConditionData>();
            ability.filters_target = Array.Empty<FilterData>(); ability.effects = Array.Empty<EffectData>();
            ability.status = Array.Empty<StatusData>(); ability.chain_abilities = Array.Empty<AbilityData>();
            var range = Make<ConditionWideAreaRange>(); range.directions = new List<Direction> { new Direction() };
            ability.condition_wide_range = range;
            AbilityData.ability_dict.Add(ability.id, ability); abilityIds.Add(ability.id);
            return ability;
        }

        [MenuItem("TcgEngine/Validate Ability Pipeline")]
        public static void Run()
        {
            CardData.Load(); AbilityData.Load(); VariantData.Load(); TraitData.Load(); StatusData.Load(); WeaponData.Load(); ClubData.Load();
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
            string directory = Path.Combine(Application.dataPath, "../.ai-work/ability-pipeline");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "pipeline-tests.json"), JsonUtility.ToJson(report, true));
            int failed = report.results.Count(r => !r.passed);
            Debug.Log("ABILITY_PIPELINE passed=" + (report.results.Count - failed) + " failed=" + failed);
            if (failed > 0) throw new Exception(string.Join("\n", report.results.Where(r => !r.passed).Select(r => r.mode + " " + r.scenario + ": " + r.error)));
        }

        private static List<TcgEngine.AI.AIAction> SelectionActions(Game game)
        {
            var ai = new TcgEngine.AI.AILogic();
            var actions = new List<TcgEngine.AI.AIAction>();
            typeof(TcgEngine.AI.AILogic).GetMethod("AddSelectActions", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ai, new object[] { actions, game, null });
            var keys = actions.Select(a => a.type + ":" + a.target_uid + ":" + a.target_player_id + ":" + a.slot_from.x + ":" + a.slot_from.y
                + ":" + a.slot.x + ":" + a.slot.y + ":" + a.slot.p + ":" + a.value).ToList();
            Check(keys.Count == keys.Distinct().Count(), "duplicate AI candidates: " + keys.Count + "/" + keys.Distinct().Count());
            return actions;
        }

        private static void Scenarios()
        {
            Case("AI target actions contain each legal player and board candidate once", () => {
                var f = new Fixture(); var c = f.Add("Target_Delete", 0, Slot.None);
                f.Add("Commuting_Student_MIDDLE", 1, S(3, 2));
                Check(f.logic.SelectPlayTarget(c, S(2, 2)), "play rejected");
                var actions = SelectionActions(f.game);
                Check(actions.Count == 3 && actions.All(a => a.type == GameAction.SelectSlot), "legal position candidates lost or duplicated");
                Check(actions.Count(a => a.slot.IsPlayerSlot()) == 2, "teacher positions");
                Check(actions.Count(a => a.slot == S(3, 2)) == 1, "occupied position");
                f.Roundtrip();
            });
            Case("Selection packets roundtrip board and teacher positions", () => {
                foreach (Slot position in new[] { S(2, 1), new Slot(0, 0, 1) }) {
                    using (var writer = new Unity.Netcode.FastBufferWriter(128, Unity.Collections.Allocator.Temp)) {
                        writer.WriteNetworkSerializable(new MsgSelectSlotRequest { slot = position });
                        using (var reader = new Unity.Netcode.FastBufferReader(writer, Unity.Collections.Allocator.Temp)) {
                            reader.ReadNetworkSerializable(out MsgSelectSlotRequest decoded);
                            Check(decoded.slot == position && reader.Position == writer.Length, "position packet mismatch");
                        }
                    }
                }
            });
            Case("Move packets roundtrip departure and destination", () => {
                using (var writer = new Unity.Netcode.FastBufferWriter(128, Unity.Collections.Allocator.Temp)) {
                    writer.WriteNetworkSerializable(new MsgSelectMoveRequest { from = S(2, 3), to = S(3, 3) });
                    using (var reader = new Unity.Netcode.FastBufferReader(writer, Unity.Collections.Allocator.Temp)) {
                        reader.ReadNetworkSerializable(out MsgSelectMoveRequest decoded);
                        Check(decoded.from == S(2, 3) && decoded.to == S(3, 3) && reader.Position == writer.Length, "move packet mismatch");
                    }
                }
            });
            Case("Move selection accepts one valid pair and pays once", () => {
                var f = new Fixture(); var unit = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var enemy = f.Add("Commuting_Student_MIDDLE", 1, S(2, 4)); var a = AbilityData.Get("activate_move_ally");
                Check(a != null && a.IsMoveSelector() && a.chain_abilities.Length == 0, "move ability not migrated");
                f.logic.TriggerCardAbility(a, unit); f.Settle();
                Check(f.game.selector == SelectorType.SelectMove && f.game.CanCancelSelector(), "move selector missing");
                var moves = a.GetSelectableMoves(f.game, unit, f.game.selector_context);
                Check(moves.Count > 0 && moves.All(m => m.from == unit.slot && unit.slot.GetNeighborSlot(1).Contains(m.to)
                    && f.game.CanMoveCard(unit, m.to, true)), "pair outside unit/destination/relation conditions");
                Check(!moves.Any(m => m.from == enemy.slot), "enemy unit accepted by ally move");
                var actions = SelectionActions(f.game);
                Check(actions.Count == moves.Count && actions.All(x => x.type == GameAction.SelectMove), "AI move candidates lost or duplicated");
                f.logic.SelectSlot(S(3, 2)); f.logic.SelectMove(unit.slot, S(2, 4)); f.logic.SelectMove(unit.slot, S(4, 2));
                Check(f.game.selector == SelectorType.SelectMove && unit.slot == S(2, 2) && f.game.players[0].mana == 100, "invalid input accepted");
                f.logic.SelectMove(unit.slot, S(3, 2)); f.Settle();
                Check(unit.slot == S(3, 2) && f.game.selector == SelectorType.None && f.game.players[0].mana == 99, "pair not applied once");
                f.Roundtrip();
            });
            Case("Move ability without a pair cannot be cast", () => {
                var f = new Fixture(); var unit = f.Add("Commuting_Student_MIDDLE", 0, S(1, 1)); var a = AbilityData.Get("activate_move_ally");
                Check(f.game.CanCastAbility(unit, a), "free neighbor rejected");
                foreach (Slot slot in unit.slot.GetNeighborSlot(1).Where(s => s != unit.slot)) f.Add("Commuting_Student_MIDDLE", 1, slot);
                Check(!a.HasValidSelectTarget(f.game, unit) && !f.game.CanCastAbility(unit, a), "cast allowed without a destination");
            });
            Case("Cancelled move selection keeps cost and position", () => {
                var f = new Fixture(); var unit = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var a = AbilityData.Get("activate_move_ally");
                f.logic.TriggerCardAbility(a, unit); f.Settle(); f.logic.CancelSelection();
                Check(f.game.selector == SelectorType.None && unit.slot == S(2, 2) && f.game.players[0].mana == 100
                    && !f.game.ability_played.Contains(a.id), "cancel moved or paid");
            });
            Case("OnPlay move keeps its pair through the play", () => {
                var f = new Fixture(); var reisa = f.Add("Uzawa_Reisa", 0, Slot.None); var enemy = f.Add("Commuting_Student_MIDDLE", 1, S(2, 3));
                var a = reisa.GetAbilities().First(x => x.IsMoveSelector());
                Check(a.IsPlaySelectTarget() && a.can_cancel, "OnPlay move ability not migrated");
                Check(f.logic.SelectPlayTarget(reisa, S(2, 2)), "play rejected");
                Check(f.game.selector == SelectorType.SelectMove && f.game.players[0].cards_board_temp.Contains(reisa), "play move selector missing");
                var moves = a.GetSelectableMoves(f.game, reisa, f.game.selector_context);
                Check(moves.Count > 0 && moves.All(m => m.from == enemy.slot && reisa.slot.GetNeighborSlot(reisa.GetRange()).Contains(m.to)
                    && f.game.CanMoveCard(enemy, m.to, true)), "enemy pair outside range or territory");
                f.logic.CancelSelection();
                Check(f.game.players[0].cards_hand.Contains(reisa) && enemy.slot == S(2, 3), "cancel did not restore card");
                Check(f.logic.SelectPlayTarget(reisa, S(2, 2)), "replay rejected");
                Slot to = moves[0].to; f.logic.SelectMove(enemy.slot, to); f.Settle();
                Check(f.game.IsOnBoard(reisa) && enemy.slot == to && f.game.selector == SelectorType.None, "pair lost through play");
                f.Roundtrip();
            });
            Case("Forced relocation triggers OnMove", () => {
                var f = new Fixture(); var unit = f.Add("Commuting_Student_MIDDLE", 0, S(3, 2)); var attacker = f.Add("Commuting_Student_MIDDLE", 1, S(2, 2));
                var onMove = Ability(AbilityTarget.Self); onMove.trigger = AbilityTrigger.OnMove; onMove.value = 1;
                var add = Make<EffectAddStat>(); add.type = EffectStatType.Attack; onMove.effects = new EffectData[] { add }; unit.AddAbility(onMove);
                int attack = unit.attack; f.logic.MoveCard(unit, S(3, 1), true); f.Settle();
                Check(unit.slot == S(3, 1) && unit.attack == attack + 1, "forced move skipped OnMove");
                var push = Ability(AbilityTarget.Self); push.value = 1; f.logic.MoveCard(unit, S(3, 2), true); f.Settle();
                Make<EffectKnockback>().DoEffect(f.logic, push, attacker, unit); f.Settle();
                Check(unit.slot == S(4, 2) && unit.attack == attack + 3, "knockback skipped OnMove");
            });
            Case("Row damage selects an empty position but only damages units", () => {
                var f = new Fixture(); var left = f.Add("Commuting_Student_MIDDLE", 0, S(1, 2)); var right = f.Add("Commuting_Student_MIDDLE", 1, S(5, 2)); left.hp = right.hp = 20;
                var spell = f.Add("Scream_Rider", 0, Slot.None);
                Check(f.logic.SelectPlayTarget(spell, S(3, 2)), "empty row center rejected"); f.Settle();
                Check(left.damage == 6 && right.damage == 6 && f.game.players.All(p => p.hp == 100), "row spillover to teacher"); f.Roundtrip();
            });
            Case("Card click and slot click produce the same unit-only effect", () => {
                foreach (bool clickCard in new[] { false, true }) {
                    var f = new Fixture(); var t = f.Add("Commuting_Student_MIDDLE", 1, S(3, 5)); t.hp = 20;
                    var spell = f.Add("Target_Delete", 0, Slot.None); f.logic.SelectPlayTarget(spell, S(2, 2)); f.Settle();
                    if (clickCard) f.logic.SelectCard(t); else f.logic.SelectSlot(t.slot);
                    f.Settle(); Check(t.damage == 10 && f.game.players[1].hp == 100, "input type changed damage recipient");
                    Check(spell.play_target_slot == t.slot && f.game.selector_context == null, "selection not stored as position"); f.Roundtrip();
                }
            });
            Case("Player clicks and player positions use one selection path", () => {
                foreach (bool clickPlayer in new[] { false, true }) {
                    var f = new Fixture(); var spell = f.Add("Target_Delete", 0, Slot.None);
                    f.logic.SelectPlayTarget(spell, S(2, 2)); f.Settle();
                    if (clickPlayer) f.logic.SelectPlayer(f.game.players[1]); else f.logic.SelectSlot(new Slot(0, 0, 1));
                    f.Settle(); f.logic.SelectSlot(new Slot(0, 0, 1)); f.Settle();
                    Check(f.game.players[1].hp == 90 && spell.play_target_slot == new Slot(0, 0, 1), "player position lost or applied twice"); f.Roundtrip();
                }
            });
            Case("Unit designation restrictions do not reject a tile effect", () => {
                var f = new Fixture(); var t = f.Add("Commuting_Student_MIDDLE", 0, S(2, 1));
                var unit = AbilityData.Get("spell_heal_ally_target"); var tile = AbilityData.Get("spell_damage2"); var c = f.Add("Angel_24_Lunchbox", 0, Slot.None);
                foreach (StatusType status in new[] { StatusType.Stealth, StatusType.SpellImmunity }) {
                    t.AddStatus(status, 1, 0); Check(!unit.CanTarget(f.game, c, t.slot) && !unit.CanTarget(f.game, c, t), "unit restriction bypassed by input");
                    Check(tile.CanTarget(f.game, c, t.slot) && tile.CanTarget(f.game, c, t), "tile depends on occupant restriction"); t.RemoveStatus(status);
                }
                t.dying = true; Check(!unit.CanTarget(f.game, c, t.slot) && tile.CanTarget(f.game, c, t.slot), "dying unit input distinction");
            });
            Case("Final conditions and filters receive projected units", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); c.hp = 20; var t = f.Add("Commuting_Student_MIDDLE", 1, S(3, 5)); t.hp = 20;
                var a = Ability(AbilityTarget.AllSlots); a.effect_target = AbilityEffectTarget.Unit; a.value = 2; a.effects = new EffectData[] { Make<EffectDamage>() };
                var cardType = Make<ConditionTarget>(); cardType.type = ConditionTargetType.Card; a.condition_target = new ConditionData[] { cardType };
                var random = Make<FilterRandom>(); random.amount = 1; a.filters_target = new FilterData[] { random };
                f.logic.TriggerCardAbility(a, c); f.Settle();
                Check(c.damage + t.damage == 2 && f.game.players.All(p => p.hp == 100), "filter/condition ran on positions or ran twice"); f.Roundtrip();
            });
            Case("Area unit recipients are frozen before the first effect", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); c.hp = 20; var t = f.Add("Commuting_Student_MIDDLE", 1, S(3, 2)); t.hp = 20;
                Card replacement = null; f.logic.onCardDamaged += (source, target, amount) => {
                    if (target != c) return;
                    f.game.players[1].cards_board.Remove(t); f.game.players[1].cards_hand.Add(t);
                    replacement = f.Add("Commuting_Student_MIDDLE", 1, S(3, 2)); replacement.hp = 20;
                };
                var a = Ability(AbilityTarget.AllSlots); a.effect_target = AbilityEffectTarget.Unit; a.value = 1; a.effects = new EffectData[] { Make<EffectDamage>() };
                f.logic.TriggerCardAbility(a, c); f.Settle();
                Check(c.damage == 1 && t.damage == 0 && replacement != null && replacement.damage == 0, "late replacement inherited a frozen target"); f.Roundtrip();
            });
            Case("Execution resolves the occupant at the selected position", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var old = f.Add("Commuting_Student_MIDDLE", 1, S(3, 2)); old.hp = 20;
                var a = Ability(AbilityTarget.SelectSlot); a.trigger = AbilityTrigger.OnPlay; a.effect_target = AbilityEffectTarget.Unit;
                a.effects = new EffectData[] { Make<EffectDamage>() }; a.value = 2; c.SetPlayTarget(old);
                old.slot = S(4, 2); var current = f.Add("Commuting_Student_MIDDLE", 1, S(3, 2)); current.hp = 20;
                f.logic.TriggerCardAbility(a, c); f.Settle();
                Check(old.damage == 0 && current.damage == 2, "selection followed UID instead of position"); f.Roundtrip();
            });
            Case("Unit projection keeps repeat and chain scopes", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 1)); c.hp = 20;
                var a = Ability(AbilityTarget.SelfSlot); a.effect_target = AbilityEffectTarget.Unit; a.value = 1; a.effects = new EffectData[] { Make<EffectDamage>() };
                var repeat = Make<RepeatStaticValue>(); repeat.value = 2; a.condition_repeat = repeat;
                var chain = Ability(AbilityTarget.ParentSlotPlayers); chain.value = 3; chain.effects = new EffectData[] { Make<EffectHeal>() }; a.chain_abilities = new[] { chain }; f.game.players[0].hp = 70;
                f.logic.TriggerCardAbility(a, c); f.Settle(); Check(c.damage == 2 && f.game.players[0].hp == 76, "projection lost parent position or repeat scope"); f.Roundtrip();
            });
            Case("AI card selector enumerates cached club candidates once", () => {
                var f = new Fixture();
                foreach (string id in new[] { "Engineering_Department", "Justice_Task_Force" }) {
                    var club = f.Add(id, 0, Slot.None);
                    f.game.players[0].cards_hand.Remove(club); f.game.players[0].cards_club.Add(club);
                }
                var c = f.Add("New_Club_Member", 0, Slot.None);
                Check(f.logic.SelectPlayTarget(c, S(2, 2)), "play rejected"); f.Settle();
                Check(f.game.selector == SelectorType.SelectorCard, "club selection missing");
                var actions = SelectionActions(f.game);
                Check(actions.Count == 2 && actions.All(a => a.type == GameAction.SelectCard), "club candidates lost");
                Check(new HashSet<string>(actions.Select(a => a.target_uid)).SetEquals(f.game.players[0].cards_club.Select(c2 => c2.uid)), "wrong club candidates");
                f.logic.SelectCard(f.game.GetCard(actions[0].target_uid)); f.Settle();
                Check(f.game.selector == SelectorType.None && f.game.IsOnBoard(c), "selection did not resume");
                f.Roundtrip();
            });
            Case("Probability uses one evaluator for every target shape and condition stage", () => {
                var f = new Fixture(); var c = f.game.players[0].cards_hand[0]; var a = Ability(AbilityTarget.Self);
                var chance = Make<ConditionPossibility>(); chance.possibility = .5f; chance.evaluation = ConditionEvaluationPolicy.EvaluateOnce;
                var shapes = new (object target, object selected)[] { (c, null), (f.game.players[0], null), (S(2, 2), null),
                    (c.CardData, null), (S(2, 2), c), (S(2, 2), S(3, 2)) };
                var expected = ((GameRandom)f.game.GetRandom()).Clone();
                foreach (ConditionStage stage in Enum.GetValues(typeof(ConditionStage))) foreach (var shape in shapes) {
                    var context = new AbilityExecutionContext(f.game, c, null, null);
                    for (int i = 0; i < 10; i++)
                        Check(ConditionEvaluation.Matches(chance, f.game, a, c, shape.target, stage, AbilityExecutionContext.Query(context), shape.selected), "possible query rejected");
                    bool roll = expected.NextDouble() < .5;
                    Check(ConditionEvaluation.Matches(chance, f.game, a, c, shape.target, stage, context, shape.selected) == roll, "target shape bypassed probability");
                    Check(ConditionEvaluation.Matches(chance, f.game, a, c, shape.target, stage, context, shape.selected) == roll, "execution rerolled");
                    Check(ConditionEvaluation.Matches(chance, f.game, a, c, shape.target, stage, AbilityExecutionContext.Query(context), shape.selected) == roll, "query lost cached result");
                }
                Check(expected.NextDouble() == f.game.GetRandom().NextDouble(), "extra probability evaluations");
            });
            Case("Probability endpoints and revalidation have explicit draw counts", () => {
                var f = new Fixture(); var c = f.game.players[0].cards_hand[0]; var a = Ability(AbilityTarget.Self);
                var chance = Make<ConditionPossibility>(); var context = new AbilityExecutionContext(f.game, c, null, null);
                var expected = ((GameRandom)f.game.GetRandom()).Clone();
                foreach (float probability in new[] { 0f, 1f }) {
                    chance.possibility = probability;
                    Check(ConditionEvaluation.Matches(chance, f.game, a, c, c, ConditionStage.Trigger, context) == (probability == 1), "endpoint result");
                }
                chance.possibility = .5f;
                for (int i = 0; i < 256; i++) {
                    bool roll = expected.NextDouble() < .5;
                    Check(ConditionEvaluation.Matches(chance, f.game, a, c, c, ConditionStage.Trigger, context) == roll, "endpoint consumed random or revalidation cached");
                }
                Check(expected.NextDouble() == f.game.GetRandom().NextDouble(), "revalidation draw count");
            });
            Case("Mashiro asset rolls once through queue registration and resolution", () => {
                var f = new Fixture(); var caster = f.Add("Shizuyama_Mashiro", 0, S(2, 2));
                var target = f.Add("Commuting_Student_MIDDLE", 1, S(2, 3)); target.hp = 100000;
                f.game.last_attacked = target.uid; f.game.last_attacked_slot = target.slot;
                var ability = caster.GetAbilities().Single(a => a.id == "OAA_damage_0.5");
                Check(ability.conditions_trigger.OfType<ConditionPossibility>().Single().evaluation == ConditionEvaluationPolicy.EvaluateOnce,
                    "Mashiro data no longer uses invocation lifetime");
                int starts = 0, expectedStarts = 0;
                f.logic.onAbilityStart += (a, _) => { if (a.id == ability.id) starts++; };
                var expected = ((GameRandom)f.game.GetRandom()).Clone();
                for (int i = 0; i < 1000; i++) {
                    if (expected.NextDouble() < .5) expectedStarts++;
                    f.logic.TriggerCardAbilityType(AbilityTrigger.OnAfterAttack, caster, target); f.Settle();
                    Check(starts == expectedStarts, "registration and execution did not share the probability decision");
                }
                Check(starts > 430 && starts < 570, "unexpected Mashiro success distribution");
                Check(expected.NextDouble() == f.game.GetRandom().NextDouble(), "Mashiro consumed extra draws");
            });

            Case("Empty card selection finalizes its candidate filters only once", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var a = Ability(AbilityTarget.CardSelector);
                var filter = Make<PipelineEmptyFilter>(); a.filters_target = new FilterData[] { filter };
                f.logic.TriggerCardAbility(a, c); f.Settle();
                Check(filter.calls == 1 && f.game.selector == SelectorType.None && f.game.ability_played.Contains(a.id), "empty selector reran filters or failed to complete");
            });
            Case("Chosen ability can suspend again without losing its queue scope", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var parent = Ability(AbilityTarget.ChoiceSelector); var child = Ability(AbilityTarget.SelectSlot);
                child.trigger = AbilityTrigger.None;
                var chain = Ability(AbilityTarget.ParentSlotPlayers); chain.value = 2; chain.effects = new EffectData[] { Make<EffectDamage>() };
                child.chain_abilities = new[] { chain }; parent.chain_abilities = new[] { child };
                f.logic.TriggerCardAbility(parent, c); f.Settle(); var parentContext = f.game.selector_context;
                f.logic.SelectChoice(0); f.Settle();
                Check(f.game.selector == SelectorType.SelectTarget && !ReferenceEquals(parentContext, f.game.selector_context), "chosen ability did not suspend with its own context");
                f.logic.SelectSlot(Slot.GetInsideSlot(1).First()); f.Settle(); f.logic.SelectChoice(0);
                Check(f.game.players[1].hp == 98 && f.game.selector == SelectorType.None && !f.logic.IsResolving(), "nested selection lost chain, duplicated input or left suspended scope");
                var next = Ability(AbilityTarget.PlayerOpponent); next.value = 1; next.effects = new EffectData[] { Make<EffectDamage>() };
                f.logic.TriggerCardAbility(next, c); f.Settle(); Check(f.game.players[1].hp == 97, "next invocation inherited stale scope");
            });
            Case("Choice queries preserve RNG and do not populate execution caches", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var choice = Ability(AbilityTarget.PlayerOpponent); var p = Make<ConditionPossibility>();
                p.possibility = .5f; choice.conditions_trigger = new ConditionData[] { p };
                f.game.selector_context = new AbilityExecutionContext(f.game, c, null, null);
                foreach (var policy in new[] { ConditionEvaluationPolicy.Revalidate, ConditionEvaluationPolicy.EvaluateOnce }) {
                    p.evaluation = policy; var expected = ((GameRandom)f.game.GetRandom()).Clone();
                    for (int i = 0; i < 20; i++) Check(f.game.CanSelectAbility(c, choice), "possible choice rejected");
                    Check(expected.NextDouble() == f.game.GetRandom().NextDouble(), "choice query consumed RNG");
                }
            });
            Case("Choice child owns a fresh cache and runs through the queue", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var parent = Ability(AbilityTarget.ChoiceSelector); var child = Ability(AbilityTarget.PlayerOpponent);
                var condition = Make<PipelineInvocationCondition>(); condition.evaluation = ConditionEvaluationPolicy.EvaluateOnce;
                parent.conditions_trigger = child.conditions_trigger = new ConditionData[] { condition };
                parent.chain_abilities = new[] { child }; child.value = 2; child.effects = new EffectData[] { Make<EffectDamage>() };
                var starts = new List<string>(); f.logic.onAbilityStart += (a, _) => starts.Add(a.id);
                f.logic.TriggerCardAbility(parent, c); f.Settle();
                var original = (AbilityExecutionContext)f.game.selector_context;
                original.RecordTarget(S(3, 2)); f.logic.SelectChoice(0); f.Settle(); f.logic.SelectChoice(0);
                Check(condition.executions.Count == 2 && !ReferenceEquals(condition.executions[0], condition.executions[1]), "parent and child shared execution");
                Check(starts.SequenceEqual(new[] { parent.id, child.id }), "child bypassed or duplicated queue dispatch");
                Check(original.resolved_slots.Contains(S(3, 2)), "child reset parent results");
                Check(condition.executions[1].parent_slots.Contains(S(3, 2)) && f.game.players[1].hp == 98, "child lost parent snapshot or repeated effects");
            });
            Case("Changed choice conditions cannot reuse the parent result", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var parent = Ability(AbilityTarget.ChoiceSelector); var child = Ability(AbilityTarget.PlayerOpponent);
                var p = Make<PipelineProbeCondition>(); p.evaluation = ConditionEvaluationPolicy.EvaluateOnce;
                parent.conditions_trigger = child.conditions_trigger = new ConditionData[] { p }; parent.chain_abilities = new[] { child };
                child.value = 2; child.effects = new EffectData[] { Make<EffectDamage>() };
                f.logic.TriggerCardAbility(parent, c); f.Settle(); p.targetResult = false; f.logic.SelectChoice(0);
                Check(f.game.players[1].hp == 100 && f.game.selector == SelectorType.SelectorChoice, "invalid choice accepted using parent cache");
                f.logic.ClearResolve();
            });
            Case("OnPlay selection keeps one evaluation through play and resolution", () => {
                var f = new Fixture(); var c = f.game.players[0].cards_hand[0]; var target = f.Add("Commuting_Student_MIDDLE", 0, S(3, 2)); target.hp = 10;
                var a = Ability(AbilityTarget.SelectSlot); a.trigger = AbilityTrigger.OnPlay; a.value = 2;
                var p = Make<PipelineInvocationCondition>(); p.evaluation = ConditionEvaluationPolicy.EvaluateOnce;
                a.conditions_criteria_target = new ConditionData[] { p }; a.effects = new EffectData[] { Make<EffectDamage>() }; c.AddAbility(a);
                Check(f.logic.SelectPlayTarget(c, S(2, 2)), "play rejected");
                var execution = f.game.selector_context as AbilityExecutionContext;
                Check(execution != null && execution.last_played == c.uid && execution.last_summoned_slot == c.slot, "planned play context missing");
                f.Roundtrip(); var clone = Game.CloneNew(f.game);
                Check(!ReferenceEquals(clone.selector_context, execution), "prediction shares selection context");
                f.logic.SelectCard(target); f.Settle(); f.logic.SelectSlot(target.slot);
                Check(p.executions.Count == 1 && ReferenceEquals(p.executions[0], execution), "acceptance and OnPlay reevaluated once-only condition");
                Check(target.damage == 2 && f.game.IsOnBoard(c) && f.game.selector_context == null, "play resumed twice or context leaked");
            });
            Case("Cancelled OnPlay selection does not leak its condition cache", () => {
                var f = new Fixture(); var c = f.game.players[0].cards_hand[0]; var a = Ability(AbilityTarget.SelectSlot);
                a.trigger = AbilityTrigger.OnPlay; a.can_cancel = true; c.AddAbility(a);
                var p = Make<PipelineInvocationCondition>(); p.evaluation = ConditionEvaluationPolicy.EvaluateOnce;
                a.conditions_criteria_target = new ConditionData[] { p };
                f.logic.SelectPlayTarget(c, S(2, 2)); var first = f.game.selector_context;
                Check(a.CanTarget(f.game, c, S(3, 2), first, true), "first evaluation"); f.logic.CancelSelection();
                Check(f.game.players[0].cards_hand.Contains(c) && f.game.selector_context == null, "cancel did not restore card");
                f.logic.SelectPlayTarget(c, S(2, 2)); var second = f.game.selector_context;
                Check(a.CanTarget(f.game, c, S(3, 2), second, true), "second evaluation");
                Check(!ReferenceEquals(first, second) && p.executions.Count == 2, "cancelled result reused"); f.logic.CancelSelection();
            });
            Case("Missing candidate cache rejects input without rerolling", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var a = Ability(AbilityTarget.CardSelector);
                var filter = Make<FilterRandom>(); filter.amount = 1; a.filters_target = new FilterData[] { filter };
                f.logic.TriggerCardAbility(a, c); f.Settle(); f.game.selector_card_uids = null;
                var expected = ((GameRandom)f.game.GetRandom()).Clone(); f.logic.SelectCard(c);
                Check(a.GetSelectionCandidates(f.game, c).Count == 0 && f.game.selector == SelectorType.SelectorCard, "missing cache recomputed");
                Check(expected.NextDouble() == f.game.GetRandom().NextDouble(), "missing cache rerolled"); f.logic.ClearResolve();
            });
            Case("Slot damage reaches its indoor teacher without a legacy discriminator", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                f.logic.DamageCard_Event(c, Slot.GetInsideSlot(1).First(), 2, true);
                Check(f.game.players[1].hp == 98, "indoor teacher missing");
                Check(typeof(GameLogic).GetMethod("DamageCard", new[] { typeof(Card), typeof(Slot), typeof(int), typeof(bool) }) == null, "legacy slot damage API remains");
                Check(typeof(EffectDamage).GetField("damage_type") == null && typeof(EffectDamageCount).GetField("damage_type") == null, "legacy discriminator remains");
            });
            Case("Four indoor tiles each damage the teacher after range deduplication", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(1, 1)); c.hp = 20;
                var a = Ability(AbilityTarget.SelfSlot); a.effects = new EffectData[] { Make<EffectDamage>() }; a.value = 2;
                a.condition_wide_range.directions.AddRange(new[] { new Direction { dx = 1 }, new Direction { dx = 2 }, new Direction { dx = 3 }, new Direction { dx = 1 } });
                var tiles = a.GetSlotTargets(f.game, c); Check(tiles.Count == 4 && tiles.Distinct().Count() == 4, "range did not finish deduplicating before execution");
                int events = 0; f.logic.onPlayerDamaged += (source, player, value) => { if (player.player_id == 0) events++; };
                f.logic.TriggerCardAbility(a, c); f.Settle();
                Check(c.damage == 2 && f.game.players[0].hp == 92 && events == 4, "per-tile damage collapsed or repeated"); f.Roundtrip();
            });
            Case("Overlapping tile ranges deduplicate before the first effect", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var a = Ability(AbilityTarget.AllSlots); a.condition_wide_range.directions.Add(new Direction { dx = 1 });
                var inside = Make<ConditionSlotLocate>(); inside.Inside = true; a.condition_target = new ConditionData[] { inside };
                a.effects = new EffectData[] { Make<EffectDamage>() }; a.value = 1;
                var tiles = a.GetSlotTargets(f.game, c); Check(tiles.Count == Slot.GetInsideSlot().Count && tiles.Distinct().Count() == tiles.Count, "overlapping final tile list");
                f.logic.TriggerCardAbility(a, c); f.Settle();
                foreach (var p in f.game.players) Check(p.hp == 100 - Slot.GetInsideSlot(p.player_id).Count, "overlap or player dedup"); f.Roundtrip();
            });
            Case("Tile heal applies per indoor tile including empty tiles", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(1, 1)); c.hp = 20; c.damage = 8; f.game.players[0].hp = 70;
                var a = Ability(AbilityTarget.SelfSlot); a.effects = new EffectData[] { Make<EffectHeal>() }; a.value = 2;
                for (int i = 1; i < 4; i++) a.condition_wide_range.directions.Add(new Direction { dx = i });
                f.logic.TriggerCardAbility(a, c); f.Settle();
                Check(c.damage == 6 && f.game.players[0].hp == 78, "heal lost empty or repeated teacher recipients"); f.Roundtrip();
            });
            Case("Tile teacher follows territory not occupant ownership", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var enemy = f.Add("Commuting_Student_MIDDLE", 1, S(2, 1)); enemy.hp = 20;
                f.logic.DamageCard_Event(c, enemy.slot, 2, true);
                Check(enemy.damage == 2 && f.game.players[0].hp == 98 && f.game.players[1].hp == 100, "occupant ownership selected teacher");
            });
            Case("Outdoor neutral and invalid tiles never reach a teacher", () => {
                var f = new Fixture(); int cards = 0, players = 0; var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                foreach (var slot in new[] { c.slot, S(2, 3), Slot.None, new Slot(0, 0, 0), new Slot(99, 1, 0) })
                    f.logic.ApplyTileEffect(slot, _ => cards++, _ => players++);
                Check(cards == 1 && players == 0, "invalid or non-indoor recipient");
            });
            Case("Unit damage and heal never project to an indoor teacher", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var t = f.Add("Commuting_Student_MIDDLE", 1, S(3, 5)); t.hp = 20;
                var a = Ability(AbilityTarget.Self); a.value = 3; f.game.players[1].hp = 70;
                Make<EffectDamage>().DoEffect(f.logic, a, c, t); Make<EffectHeal>().DoEffect(f.logic, a, c, t);
                Check(t.damage == 0 && f.game.players[1].hp == 70, "card route leaked to teacher");
            });
            Case("Angel 24 selects a position but heals only its unit", () => {
                var f = new Fixture(); var t = f.Add("Commuting_Student_MIDDLE", 0, S(2, 1)); t.hp = 20; t.damage = 8; f.game.players[0].hp = 70;
                var spell = f.Add("Angel_24_Lunchbox", 0, Slot.None); var a = spell.GetAbilities().First();
                Check(a.criteria_target == AbilityTarget.SelectSlot && a.effect_target == AbilityEffectTarget.Unit
                    && a.CanTarget(f.game, spell, t) && a.CanTarget(f.game, spell, t.slot)
                    && !a.CanTarget(f.game, spell, f.game.players[0]) && !a.CanTarget(f.game, spell, S(3, 1)), "unit position criteria");
                f.logic.SelectPlayTarget(spell, t.slot); f.Settle(); Check(f.game.selector != SelectorType.None, "selector missing");
                f.logic.SelectSlot(S(3, 1)); Check(f.game.selector != SelectorType.None && t.damage == 8, "empty slot accepted");
                var actions = SelectionActions(f.game); Check(actions.Count > 0 && actions.All(x => x.type == GameAction.SelectSlot), "AI must select positions");
                f.logic.SelectSlot(t.slot); f.Settle(); f.logic.SelectCard(t); f.Settle();
                Check(t.damage == 5 && f.game.players[0].hp == 70 && f.game.selector == SelectorType.None, "unit heal leaked or applied twice"); f.Roundtrip();
            });
            Case("Numeric conversion is evaluated independently for tile unit and teacher", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var izumi = f.Add("Shishido_Izumi", 1, S(3, 5)); izumi.damage = 3;
                f.logic.DamageCard_Event(c, izumi.slot, 2, true);
                Check(izumi.damage == 1 && f.game.players[1].hp == 98, "unit conversion leaked into teacher request"); f.Roundtrip();
            });
            Case("Repeated tile effects and effects array remain independent", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 1)); c.hp = 30;
                var a = Ability(AbilityTarget.SelfSlot); a.effects = new EffectData[] { Make<EffectDamage>(), Make<EffectDamage>() }; a.value = 1;
                var repeat = Make<RepeatStaticValue>(); repeat.value = 2; a.condition_repeat = repeat;
                f.logic.TriggerCardAbility(a, c); f.Settle(); Check(c.damage == 4 && f.game.players[0].hp == 96, "repeat or effect was deduplicated"); f.Roundtrip();
            });
            Case("Count ratio and stored-position damage use the tile wrapper", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var t = f.Add("Commuting_Student_MIDDLE", 1, S(3, 5)); t.hp = 20;
                var a = Ability(AbilityTarget.SelfSlot); a.value = 2;
                var count = Make<EffectDamageCount>(); count.pile = PileType.Hand; count.player = EffectPlayerType.Player;
                count.DoEffect(f.logic, a, c, t.slot); Check(t.damage == 2 && f.game.players[1].hp == 98, "count route");
                var ratio = Make<EffectDamageRatio>(); ratio.ratio = 0.5f; ratio.DoEffect(f.logic, a, c, t.slot);
                Check(t.damage == 11 && f.game.players[1].hp == 49, "ratio must use each recipient health");
                var stored = Make<EffectDamage>(); stored.stored_slot = ScriptableObject.CreateInstance<MarkerData>(); stored.stored_slot.id = "stored_slot";
                c.SetMarker(stored.stored_slot.id, t.slot.ToMarkerCode()); stored.DoEffect(f.logic, a, c);
                Check(t.damage == 13 && f.game.players[1].hp == 47, "stored-position route"); f.Roundtrip();
            });
            Case("Tile death resolves after the same tile teacher result", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var t = f.Add("Commuting_Student_MIDDLE", 1, S(3, 5)); t.hp = 1;
                var a = Ability(AbilityTarget.AllSlots); var inside = Make<ConditionSlotLocate>(); inside.Inside = true; a.condition_target = new ConditionData[] { inside };
                a.effects = new EffectData[] { Make<EffectDamage>() }; a.value = 1;
                int hpAtDeath = -1; f.logic.onCardDiscarded += card => { if (card == t) hpAtDeath = f.game.players[1].hp; };
                f.logic.TriggerCardAbility(a, c); f.Settle();
                Check(!f.game.IsOnBoard(t) && f.game.players[1].hp == 100 - Slot.GetInsideSlot(1).Count, "death interrupted tile results");
                Check(hpAtDeath == f.game.players[1].hp, "death before final tile result"); f.Roundtrip();
            });
            Case("Sibling chains use the same parent slots", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var parent = Ability(AbilityTarget.AllSlots);
                var first = Ability(AbilityTarget.ParentSlotPlayers); var second = Ability(AbilityTarget.ParentSlotPlayers);
                var inside = Make<ConditionSlotLocate>(); inside.Inside = true;
                first.conditions_criteria_target = second.conditions_criteria_target = new ConditionData[] { inside };
                first.effects = second.effects = new EffectData[] { Make<EffectDamage>() };
                first.value = 1; second.value = 2; parent.chain_abilities = new[] { first, second };
                f.logic.TriggerCardAbility(parent, c); f.Settle();
                Check(f.game.players.All(p => p.hp == 97), "sibling snapshot replaced or player hit per tile");
                f.Roundtrip();
            });
            Case("Player event subject survives enqueue and resolution", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var a = Ability(AbilityTarget.PlayerOpponent); var condition = Make<PipelinePlayerCondition>();
                a.conditions_trigger = new ConditionData[] { condition }; a.effects = new EffectData[] { Make<EffectDamage>() }; a.value = 2;
                f.logic.TriggerCardAbility(a, c, f.game.players[1]); f.Settle();
                Check(condition.calls == 2 && f.game.players[1].hp == 98, "player event rechecked as caster");
            });
            Case("Cached card selection revalidates without drawing filters again", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var a = Ability(AbilityTarget.CardSelector); var citizen = Make<ConditionCardType>();
                citizen.has_type = new List<CardType> { CardType.Student, CardType.NonStudent };
                citizen.has_club = new List<ClubData>(); citizen.has_trait = new List<TraitData>();
                a.conditions_criteria_target = new ConditionData[] { citizen }; var filter = Make<FilterRandom>(); filter.amount = 1;
                a.filters_target = new FilterData[] { filter };
                f.logic.TriggerCardAbility(a, c); f.Settle();
                var selected = f.game.GetCard(f.game.selector_card_uids[0]); selected.SetCard(CardData.Get("Makoto_Statue"), selected.VariantData);
                var expected = ((GameRandom)f.game.GetRandom()).Clone(); f.logic.SelectCard(selected);
                Check(f.game.selector == SelectorType.SelectorCard, "transformed target accepted");
                Check(f.game.GetRandom().NextDouble() == expected.NextDouble(), "selection rerolled filter");
                f.logic.ClearResolve();
            });
            Case("Movement shares destination constraints", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                Check(f.game.CanMoveCard(c, S(3, 2)), "own territory");
                Check(f.game.CanMoveCard(c, S(2, 3)), "neutral territory");
                Check(!f.game.CanMoveCard(c, S(2, 4)), "opponent territory");
                Check(!f.game.CanMoveCard(c, c.slot), "same slot");
                f.Add("Commuting_Student_MIDDLE", 0, S(3, 2)); Check(!f.game.CanMoveCard(c, S(3, 2)), "occupied slot");
                var place = f.Add("Makoto_Statue", 0, S(1, 1));
                Check(!f.game.CanMoveCard(place, Slot.GetInsideSlot(0).First()), "place indoor restriction");
            });
            Case("Placement query does not create cards or consume random", () => {
                var f = new Fixture(); var c = f.game.players[0].cards_hand[0];
                var condition = Make<ConditionCanPlace>(); condition.place_card = CardData.Get("Peroro_Tlush");
                var ability = Ability(AbilityTarget.AllSlots);
                int count = f.game.players[0].cards_all.Count;
                var random = ((GameRandom)f.game.GetRandom()).Clone();
                for (int i = 0; i < 20; i++) Check(condition.IsTargetConditionMet(f.game, ability, c, S(2, 2)), "query succeeds");
                Check(count == f.game.players[0].cards_all.Count, "registry mutated");
                Check(random.NextDouble() == f.game.GetRandom().NextDouble(), "random consumed");
            });
            Case("Self only returns its card", () => {
                var f = new Fixture(); var c = f.Add("Wanibuchi_Akari", 0, S(2, 2)); var a = Ability(AbilityTarget.Self);
                Check(a.GetCardTargets(f.game, c).SequenceEqual(new[] { c }), "self card");
                Check(a.GetSlotTargets(f.game, c).Count == 0, "self duplicated into slot");
                a.criteria_target = AbilityTarget.SelfSlot;
                Check(a.GetCardTargets(f.game, c).Count == 0 && a.GetSlotTargets(f.game, c).SequenceEqual(new[] { c.slot }), "self slot");
            });
            Case("Slot selection normalizes occupied clicks and resumes once", () => {
                var f = new Fixture(); var caster = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2));
                var target = f.Add("Commuting_Student_MIDDLE", 1, S(3, 4)); target.hp = 30;
                var a = Ability(AbilityTarget.SelectSlot); a.value = 2; a.effects = new[] { Make<EffectDamage>() };
                target.AddStatus(StatusType.Stealth, 0, 0);
                Check(a.CanTarget(f.game, caster, target) == a.CanTarget(f.game, caster, target.slot), "click-dependent validity");
                Check(!a.CanTarget(f.game, caster, f.game.players[1]), "player accepted as slot");
                f.logic.TriggerCardAbility(a, caster); f.Settle(); f.Roundtrip();
                f.logic.SelectCard(target); f.Settle(); f.logic.SelectCard(target); f.Settle();
                Check(target.damage == 2 && f.game.selector == SelectorType.None, "selection applied twice or did not resolve");
            });
            Case("Random remainder returns zero when excluded count covers candidates", () => {
                var f = new Fixture(); var filter = Make<FilterRandom>(); filter.rest = true; filter.amount = 2;
                var a = Ability(AbilityTarget.AllSlots); var caster = f.game.players[0].cards_hand[0];
                Check(filter.FilterTargets(f.game, a, caster, new List<Slot> { S(2, 2) }, new List<Slot>()).Count == 0, "underflow");
                Check(filter.FilterTargets(f.game, a, caster, new List<Slot> { S(2, 2), S(3, 2) }, new List<Slot>()).Count == 0, "equal count");
            });
            Case("Predicate OR combines complete children", () => {
                var f = new Fixture(); var c = f.game.players[0].cards_hand[0]; var a = Ability(AbilityTarget.Self);
                var left = Make<PipelineProbeCondition>(); left.triggerResult = true; left.targetResult = false;
                var right = Make<PipelineProbeCondition>(); right.triggerResult = false; right.targetResult = true;
                var either = Make<ConditionCompositeOr>(); either.any = new ConditionData[] { left, right };
                a.conditions_trigger = new ConditionData[] { either };
                Check(!a.AreTriggerConditionsMet(f.game, c), "split predicates incorrectly accepted");
                right.triggerResult = true; Check(a.AreTriggerConditionsMet(f.game, c), "complete child rejected");
            });
            Case("EvaluateOnce is invocation-local; revalidation observes changes", () => {
                var f = new Fixture(); var c = f.game.players[0].cards_hand[0]; var a = Ability(AbilityTarget.Self);
                var p = Make<PipelineProbeCondition>(); p.evaluation = ConditionEvaluationPolicy.EvaluateOnce;
                a.conditions_trigger = new ConditionData[] { p }; var execution = new AbilityExecutionContext(f.game, c, null, null);
                Check(a.AreTriggerConditionsMet(f.game, c, execution), "initial"); p.targetResult = false;
                Check(a.AreTriggerConditionsMet(f.game, c, execution) && p.calls == 1, "rechecked cached condition");
                Check(!a.AreTriggerConditionsMet(f.game, c, new AbilityExecutionContext(f.game, c, null, null)), "cache leaked into next invocation");
                p.evaluation = ConditionEvaluationPolicy.Revalidate;
                Check(!a.AreTriggerConditionsMet(f.game, c, execution), "revalidation ignored state");
            });
            Case("Probability is queried without rolls and rolled once per invocation", () => {
                var f = new Fixture(); var c = f.game.players[0].cards_hand[0]; var a = Ability(AbilityTarget.Self);
                var p = Make<ConditionPossibility>(); p.possibility = .5f; p.evaluation = ConditionEvaluationPolicy.EvaluateOnce;
                a.conditions_trigger = new ConditionData[] { p }; var expected = ((GameRandom)f.game.GetRandom()).Clone();
                for (int i = 0; i < 50; i++) Check(a.AreTriggerConditionsMet(f.game, c), "possible query rejected");
                var selector = Ability(AbilityTarget.SelectSlot); selector.conditions_criteria_target = new ConditionData[] { p };
                var suspended = new AbilityExecutionContext(f.game, c, null, null);
                for (int i = 0; i < 50; i++) Check(selector.CanTarget(f.game, c, S(2, 2), suspended), "possible selection query rejected");
                int wins = 0;
                for (int i = 0; i < 4000; i++) {
                    bool roll = expected.NextDouble() < .5; var execution = new AbilityExecutionContext(f.game, c, null, null);
                    bool first = a.AreTriggerConditionsMet(f.game, c, execution), second = a.AreTriggerConditionsMet(f.game, c, execution);
                    Check(first == roll && second == first, "roll repeated or query consumed random"); if (first) wins++;
                }
                Check(wins > 1800 && wins < 2200, "probability distribution");
            });
            Case("Creation overflow and summon preparation use distinct destinations", () => {
                var f = new Fixture(); var p = f.game.players[0]; var definition = CardData.Get("Peroro_Tlush");
                while (p.cards_hand.Count < GameplayData.Get().cards_max) f.logic.CreateCardToPile(definition, VariantData.GetDefault(), p, PileType.Hand);
                int count = p.cards_all.Count; var a = Ability(AbilityTarget.AllCardData);
                var create = Make<EffectCreate>(); create.create_pile = PileType.Hand; create.DoEffect(f.logic, a, p.cards_hand[0], definition);
                var created = f.logic.SummonCardHand(p, definition, VariantData.GetDefault());
                Check(p.cards_discard.Count == 2 && p.cards_discard.Contains(created) && p.cards_all.Count == count + 2, "overflow lost or duplicated");
                Card summoned = f.logic.SummonCard(p, definition, VariantData.GetDefault(), S(2, 2)); f.Settle();
                Check(f.game.IsOnBoard(summoned) && p.cards_hand.Count == GameplayData.Get().cards_max && p.cards_temp.Count == 0, "board summon used hand capacity");
                f.Roundtrip();
            });
            Case("Overlapping ranges deduplicate recipients", () => {
                var f = new Fixture(); var c = f.Add("Commuting_Student_MIDDLE", 0, S(2, 2)); var other = f.Add("Commuting_Student_MIDDLE", 0, S(3, 2));
                var a = Ability(AbilityTarget.AllCardsBoard); a.condition_wide_range.directions.Add(new Direction { dx = 1 });
                var targets = a.GetCardTargets(f.game, c);
                Check(targets.Count == 2 && targets.Distinct().Count() == 2 && targets.Contains(other), "overlap duplicated recipient");
            });
            Case("Parent slots retain empty tiles and map territory once", () => {
                var f = new Fixture(); var c = f.game.players[0].cards_hand[0]; var parent = new AbilityExecutionContext(f.game, c, null, null);
                foreach (Slot slot in Slot.GetInsideSlot(1).Take(2)) parent.RecordTarget(slot);
                var child = new AbilityExecutionContext(f.game, c, null, parent); parent.resolved_slots.Clear();
                var a = Ability(AbilityTarget.ParentSlotPlayers); var inside = Make<ConditionSlotLocate>(); inside.Inside = true;
                a.conditions_criteria_target = new ConditionData[] { inside };
                Check(a.GetPlayerTargets(f.game, c, context: child).SequenceEqual(new[] { f.game.players[1] }), "empty tiles lost, recipient duplicated, or parent mutated");
                f.game.selector_context = child; f.Roundtrip();
            });
            Case("Game clone forks RNG and execution cache", () => {
                var f = new Fixture(); var c = f.game.players[0].cards_hand[0]; var a = Ability(AbilityTarget.Self);
                var p = Make<PipelineProbeCondition>(); p.evaluation = ConditionEvaluationPolicy.EvaluateOnce;
                a.conditions_trigger = new ConditionData[] { p }; f.game.selector_context = new AbilityExecutionContext(f.game, c, null, null);
                Check(a.AreTriggerConditionsMet(f.game, c, f.game.selector_context), "cache setup");
                Game clone = Game.CloneNew(f.game); p.targetResult = false;
                Check(a.AreTriggerConditionsMet(clone, clone.GetCard(c.uid), clone.selector_context), "cached result not cloned");
                Check(f.game.GetRandom().NextDouble() == clone.GetRandom().NextDouble(), "random state not cloned");
                var expected = ((GameRandom)f.game.GetRandom()).Clone(); clone.GetRandom().NextDouble();
                Check(expected.NextDouble() == f.game.GetRandom().NextDouble(), "prediction consumed live random");
                ((AbilityExecutionContext)clone.selector_context).RecordTarget(S(2, 2));
                Check(((AbilityExecutionContext)f.game.selector_context).resolved_slots.Count == 0, "mutable execution shared");
            });
        }
    }

    public sealed class PipelineEmptyFilter : FilterData
    {
        public int calls;
        public override List<Card> FilterTargets(Game data, AbilityData ability, Card caster, List<Card> source, List<Card> dest)
        {
            calls++; dest.Clear(); return dest;
        }
    }

    public sealed class PipelineInvocationCondition : ConditionData
    {
        public readonly List<AbilityExecutionContext> executions = new List<AbilityExecutionContext>();
        private bool Record(AbilityEventContext context) {
            if (context is AbilityExecutionContext execution && !execution.is_query) executions.Add(execution);
            return true;
        }
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target, AbilityEventContext context = null) { return Record(context); }
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target, AbilityEventContext context = null) { return Record(context); }
    }

    public sealed class PipelineProbeCondition : ConditionData
    {
        public bool triggerResult = true, targetResult = true;
        public int calls;
        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster, AbilityEventContext context = null) { return triggerResult; }
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target, AbilityEventContext context = null) { calls++; return targetResult; }
    }

    public sealed class PipelinePlayerCondition : ConditionData
    {
        public int calls;
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target, AbilityEventContext context = null) { calls++; return target.player_id == 1; }
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target, AbilityEventContext context = null) { throw new Exception("Player source became Card"); }
    }
}
#endif
