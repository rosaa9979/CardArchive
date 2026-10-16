using System;
using System.Collections.Generic;
using System.Linq;
using TcgEngine.Gameplay;
using TcgEngine.Replay;
using UnityEditor;
using UnityEngine;

namespace TcgEngine.Editor
{
    // Exercise the actual rule/effect/recorder path; isolated PresentationState tests
    // cannot detect a result attached to the wrong animation in an earlier frame.
    public static class AbilityPresentationValidation
    {
        [MenuItem("TcgEngine/FX/Validate Ability Presentation Boundaries")]
        public static void Validate() => Debug.Log("Ability presentation checks passed: " + Run());

        public static int Run()
        {
            CardData.Load();
            VariantData.Load();
            WeaponData.Load();
            AbilityData.Load();
            int checks = 0;
            Action<bool, string> check = (ok, label) => {
                if (!ok) throw new Exception(label);
                checks++;
            };
            var asset = AssetDatabase.LoadAssetAtPath<AbilityData>("Assets/TcgEngine/Resources/Abilities/play/OP_damage3_PS.asset");
            check(asset != null && asset.target_fx != null, "Drone uses an authored target FX");
            var ability = UnityEngine.Object.Instantiate(asset);
            var area = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AbilityData>(
                "Assets/TcgEngine/Resources/Abilities/spells/spell_damage1_all_enemy.asset"));
            var draw = ScriptableObject.CreateInstance<EffectDraw>();
            try
            {
                check(area.playback_mode == AbilityPlaybackMode.Simultaneous, "Existing abilities default to simultaneous playback");
                var simultaneous = new Recording(true);
                simultaneous.logic.ResolveAbility(area, simultaneous.caster);
                var areaFrames = simultaneous.Take();
                check(areaFrames.Count == 1, "Full simultaneous ability closes exactly one presentation frame");
                check(areaFrames[0].Count(GameAction.CardDamaged) == 2 && areaFrames[0].Count(GameAction.AbilityTargetCard) == 2,
                    "Both target explosions and floating results belong to the one ability snapshot");
                check(areaFrames[0].game.GetCard("victim-a").GetHP() == 3 && areaFrames[0].game.GetCard("victim-b").GetHP() == 3,
                    "Simultaneous snapshot includes all victims");
                check(simultaneous.ReplayFramesMatch(areaFrames), "Simultaneous replay uses the same single boundary");
                string areaFinalHash = ReplayStateCodec.Hash(ReplayStateCodec.Capture(simultaneous.game));

                area.playback_mode = AbilityPlaybackMode.Sequential;
                var sequential = new Recording(true);
                sequential.logic.ResolveAbility(area, sequential.caster);
                var sequentialFrames = sequential.Take();
                var hits = sequentialFrames.Where(f => f.Has(GameAction.CardDamaged)).ToList();
                check(hits.Count == 2 && hits.All(f => f.Count(GameAction.CardDamaged) == 1 && f.Count(GameAction.AbilityTargetCard) == 1),
                    "Sequential mode pairs each target's damage and explosion in its own frame");
                check(hits[0].game.GetCard("victim-a").GetHP() + hits[0].game.GetCard("victim-b").GetHP() == 7
                    && hits[1].game.GetCard("victim-a").GetHP() == 3 && hits[1].game.GetCard("victim-b").GetHP() == 3,
                    "First sequential snapshot contains no future target damage");
                check(ReplayStateCodec.Hash(ReplayStateCodec.Capture(sequential.game)) == areaFinalHash,
                    "Playback mode changes no authoritative rule result");
                check(sequential.ReplayFramesMatch(sequentialFrames), "Sequential replay preserves every target boundary");

                // Adjacent executions of the same asset must never collapse into one result.
                area.playback_mode = AbilityPlaybackMode.Simultaneous;
                simultaneous.logic.ResolveAbility(area, simultaneous.caster);
                var repeated = simultaneous.Take();
                check(repeated.Count == 1 && repeated[0].game.GetCard("victim-a").GetHP() == 2,
                    "A second execution owns a separate complete snapshot");
                areaFrames.AddRange(repeated);
                check(simultaneous.ReplayFramesMatch(areaFrames), "Repeated ability executions retain separate replay boundaries");

                area.value = 4;
                var lethal = new Recording(true);
                lethal.logic.ResolveAbility(area, lethal.caster);
                var lethalFrames = lethal.Take();
                check(lethalFrames.Count == 1 && lethalFrames[0].game.players[1].cards_board.Count == 2,
                    "Lethal area hit is captured before the independent death wave");
                check(lethal.logic.DeathStep(), "Area damage produces a following death wave");
                var deathFrames = lethal.Take();
                check(deathFrames.Count == 1 && deathFrames[0].Count(GameAction.CardDiscarded) == 2,
                    "Simultaneous deaths remain their own snapshot");
                lethalFrames.AddRange(deathFrames);
                check(lethal.ReplayFramesMatch(lethalFrames), "Area damage and death replay boundaries match live");

                var run = new Recording();
                // Stop resolution so no following ability can accidentally close the spawn.
                run.logic.PlayCard(run.caster, Slot.GetAll(0).First(), true);
                var frames = run.Take();
                check(frames.Count == 1 && frames[0].Has(GameAction.CardPlayed), "Summon closes immediately without a following ability");
                check(frames[0].game.IsOnBoard(frames[0].game.GetCard(run.caster.uid)), "Completed summon snapshot contains the new unit");
                run.logic.Resolve(ability, run.caster, run.game.players[0]);
                frames.AddRange(run.Take());
                check(frames.Count == 2, "Summon and ability are separate frames");
                check(frames[0].game.players[0].hp == 20 && frames[0].Has(GameAction.CardPlayed), "Spawn owns no future damage");
                check(frames[1].game.players[0].hp == 17 && frames[1].Has(GameAction.PlayerDamaged)
                    && frames[1].Has(GameAction.AbilityTargetPlayer), "Damage, floating event and explosion share one frame");
                check(run.ReplayFramesMatch(frames), "Replay preserves the same presentation boundaries");

                run = new Recording();
                run.logic.Resolve(ability, run.caster, run.game.players[0]);
                frames = run.Take();
                check(frames.Count == 1 && frames[0].game.players[0].hp == 17, "Ability target closes without a following target");
                run.logic.Resolve(ability, run.caster, run.game.players[1]);
                frames.AddRange(run.Take());
                check(frames.Count == 2 && frames[0].game.players[1].hp == 20 && frames[1].game.players[1].hp == 17,
                    "Area ability targets retain separate final snapshots");
                check(run.ReplayFramesMatch(frames), "Area ability replay preserves target boundaries");

                run = new Recording();
                run.logic.PlayCard(run.caster, Slot.GetAll(0).First(), true);
                var other = new Card(run.caster.card_id, "second-victim", 0) { hp = 4 };
                run.game.players[0].cards_all.Add(other.uid, other);
                run.logic.PlayCard(other, Slot.GetAll(0).Skip(1).First(), true);
                frames = run.Take();
                run.logic.MarkDying(run.caster, run.caster);
                run.logic.MarkDying(run.caster, other);
                check(run.logic.DeathStep(), "Real death creation step processes the wave");
                var deaths = run.Take();
                check(deaths.Count == 1 && deaths[0].game.players[0].cards_board.Count == 0
                    && deaths[0].game.players[0].cards_discard.Count == 2, "Simultaneous deaths close together before any next ability");
                check(deaths[0].events.Count(e => e == Convert.ToBase64String(BitConverter.GetBytes(GameAction.CardDiscarded))) == 2,
                    "Both death events belong to the completed death snapshot");
                frames.AddRange(deaths);
                check(run.ReplayFramesMatch(frames), "Death replay matches completed step snapshots");

                // Multiple EffectData and nested explicit boundaries must not split a target FX result.
                ability.effects = new EffectData[] { asset.effects[0], draw, asset.effects[0] };
                ability.value = 1;
                run = new Recording();
                run.logic.Resolve(ability, run.caster, run.game.players[0]);
                frames = run.Take();
                check(frames.Count == 1 && frames[0].game.players[0].hp == 17, "Compound damage/draw/damage is atomic under its target FX");
                check(frames[0].Has(GameAction.ExhaustDamage) && frames[0].Has(GameAction.AbilityTargetPlayer), "Nested draw cannot publish before the target cue");
                check(run.ReplayFramesMatch(frames), "Compound replay keeps its target cue with every result");

                // Draw-only abilities without a target animation still own per-card outcomes.
                ability.target_fx = null;
                ability.effects = new EffectData[] { draw };
                ability.value = 2;
                run = new Recording();
                run.logic.Resolve(ability, run.caster, run.game.players[0]);
                frames = run.Take();
                var draws = frames.Where(f => f.Has(GameAction.ExhaustDamage)).ToList();
                check(draws.Count == 2 && draws[0].game.players[0].hp == 19 && draws[1].game.players[0].hp == 17,
                    "Draw outcomes without target FX retain individual boundaries");
                check(run.ReplayFramesMatch(frames), "Draw replay matches live boundaries");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ability);
                UnityEngine.Object.DestroyImmediate(draw);
                UnityEngine.Object.DestroyImmediate(area);
            }
            return checks;
        }

        private sealed class Logic : GameLogic
        {
            public Logic(Game game) : base(game) { }
            public void Resolve(AbilityData ability, Card caster, Player target) => ResolveEffectTarget(ability, caster, target);
            public void ResolveAbility(AbilityData ability, Card caster) => ResolveCardAbility(ability, caster, caster, 1, 0);
            public bool DeathStep() => ProcessDeathStep();
        }

        private sealed class Frame
        {
            public Game game;
            public List<string> events;
            public bool Has(ushort tag) => events.Contains(Convert.ToBase64String(BitConverter.GetBytes(tag)));
            public int Count(ushort tag) => events.Count(e => e == Convert.ToBase64String(BitConverter.GetBytes(tag)));
        }

        private sealed class Recording
        {
            public readonly Game game = new Game("ability-presentation-test", 2) { state = GameState.Play, phase = GamePhase.Main };
            public readonly Card caster = new Card("Rampaging_Border_Drone", "drone", 0) { hp = 4 };
            public readonly Logic logic;
            private readonly PresentationRecorder presentation = new PresentationRecorder();
            private readonly ReplayRecorder replay = new ReplayRecorder();
            private readonly ReplayState initial;
            private ReplayState readState;

            public Recording(bool areaTargets = false)
            {
                game.players[0].hp = game.players[0].hp_max = 20;
                game.players[1].hp = game.players[1].hp_max = 20;
                game.players[0].cards_all.Add(caster.uid, caster);
                if (areaTargets)
                {
                    game.players[0].cards_hand.Add(caster);
                    var reserve = new Card(caster.card_id, "reserve", 1) { hp = 4 };
                    game.players[1].cards_all.Add(reserve.uid, reserve);
                    game.players[1].cards_hand.Add(reserve);
                    var slots = Slot.GetAll(1).Take(2).ToArray();
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var victim = new Card(caster.card_id, i == 0 ? "victim-a" : "victim-b", 1) { hp = 4, slot = slots[i] };
                        game.players[1].cards_all.Add(victim.uid, victim);
                        game.players[1].cards_board.Add(victim);
                    }
                }
                initial = ReplayStateCodec.Capture(game);
                readState = ReplayStateCodec.Copy(initial);
                presentation.Begin(game);
                replay.Begin(game, "validation");
                logic = new Logic(game);
                logic.SuspendResolution(true);
                logic.onReplayBoundary += replay.Capture;
                logic.onPresentationBoundary += g => { presentation.Capture(g); replay.PresentationBoundary(g); };
                logic.onPlayerDamaged += (c, p, n) => Emit(GameAction.PlayerDamaged);
                logic.onCardDamaged += (c, t, n) => Emit(GameAction.CardDamaged);
                logic.onExhaustDamage += p => Emit(GameAction.ExhaustDamage);
                logic.onAbilityTargetPlayer += (a, c, p) => Emit(GameAction.AbilityTargetPlayer);
                logic.onAbilityTargetCard += (a, c, t) => Emit(GameAction.AbilityTargetCard);
                logic.onAbilityTargetSlot += (a, c, s) => Emit(GameAction.AbilityTargetSlot);
                logic.onAbilityStart += (a, c) => Emit(GameAction.AbilityTrigger);
                logic.onAbilityEnd += (a, c) => Emit(GameAction.AbilityEnd);
                logic.onCardPlayed += (c, s, automatic) => Emit(GameAction.CardPlayed);
                logic.onCardDiscarded += c => Emit(GameAction.CardDiscarded);
            }

            public void Emit(ushort tag)
            {
                byte[] packet = BitConverter.GetBytes(tag);
                replay.Event(game, packet);
                presentation.Event(tag, packet);
            }

            public List<Frame> Take()
            {
                var frames = new List<Frame>();
                foreach (var frame in presentation.Take().frames)
                {
                    readState = ReplayStateCodec.Apply(readState, frame.changes);
                    frames.Add(new Frame { game = ReplayStateCodec.Restore(readState), events = frame.events });
                }
                return frames;
            }

            public bool ReplayFramesMatch(List<Frame> frames)
            {
                var state = ReplayStateCodec.Copy(initial);
                var events = new List<string>();
                int index = 0;
                foreach (var entry in replay.Record.events)
                {
                    if (entry.kind == "state") state = ReplayStateCodec.Apply(state, entry.changes);
                    if (entry.kind == "event") events.Add(entry.packet);
                    if (entry.kind != "presentation") continue;
                    if (index >= frames.Count || !events.SequenceEqual(frames[index].events)
                        || ReplayStateCodec.Hash(state) != ReplayStateCodec.Hash(ReplayStateCodec.Capture(frames[index].game))) return false;
                    index++;
                    events.Clear();
                }
                return !replay.Failed && index == frames.Count;
            }
        }
    }
}
