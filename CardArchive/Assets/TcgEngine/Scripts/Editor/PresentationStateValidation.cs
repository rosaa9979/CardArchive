using System;
using TcgEngine.Client;
using TcgEngine.Replay;
using TcgEngine.FX;
using UnityEditor;
using UnityEngine;

namespace TcgEngine.Editor
{
    public static class PresentationStateValidation
    {
        [MenuItem("TcgEngine/FX/Validate Atomic Presentation State")]
        public static void Validate() { Debug.Log("Presentation checks passed: " + RunPure()); }

        [MenuItem("TcgEngine/FX/Validate Continue Playback")]
        public static void ValidateContinuePlayback() { Debug.Log("Continue playback checks passed: " + RunContinuePlayback()); }

        // Transient Animator/clip objects only: never adds Continue to a project asset.
        public static int RunContinuePlayback()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run isolated Continue validation outside Play Mode.");
            int checks = 0;
            Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception(name); checks++; };
            GameObject root = null;
            AnimationClip clip = null;
            AnimatorOverrideController controller = null;
            PresentationClock.Reset();
            try
            {
                PresentationClock.Active = true;
                PresentationClock.Begin();
                PresentationClock.HoldEventMinimum(60f);
                bool otherOperationDone = false;
                PresentationClock.Track(() => otherOperationDone);
                PresentationClock.ContinueCurrentStep();
                check(!PresentationClock.Complete, "Continue cannot skip an unrelated tracked operation");
                otherOperationDone = true;
                check(PresentationClock.Complete, "Continue bypasses artificial padding and event minimums");
                PresentationClock.Begin(); PresentationClock.Hold(60f); PresentationClock.ContinueCurrentStep();
                check(!PresentationClock.Complete, "Continue preserves real timed callbacks");
                PresentationClock.Reset();

                var template = Resources.Load<RuntimeAnimatorController>("Presentation/StateCue");
                clip = UnityEngine.Object.Instantiate(template.animationClips[0]);
                clip.name = "Continue validation (unsaved)";
                clip.SetCurve("", typeof(FXPlayback), "cueProgress", AnimationCurve.Linear(0f, 0f, 10f, 1f));
                clip.events = new[] {
                    new AnimationEvent { time = 0.1f, functionName = "Continue" },
                    new AnimationEvent { time = 0.2f, functionName = "Impact", intParameter = 0 },
                    new AnimationEvent { time = 0.35f, functionName = "Continue" },
                    new AnimationEvent { time = 10f, functionName = "Complete" }
                };
                controller = new AnimatorOverrideController(template);
                controller[template.animationClips[0].name] = clip;
                root = new GameObject("Continue validation (unsaved)") { hideFlags = HideFlags.HideAndDontSave };
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                var playback = FXPlayback.Prepare(root);
                int impacts = 0, continuations = 0, completions = 0;
                playback.Bind(_ => impacts++, () => completions++, () => continuations++);
                playback.Continue(new AnimationEvent());
                check(continuations == 0, "A fabricated/unarmed Continue is ignored");
                playback.Arm();
                animator.Update(0f); animator.Update(0.15f);
                check(impacts == 0 && continuations == 0 && playback.IsPlaying,
                    "A real Animator Continue before Impact remains deferred");
                animator.Update(0.1f);
                check(impacts == 1 && continuations == 1 && completions == 0 && playback.IsPlaying,
                    "Real Impact releases a deferred Continue without ending the visual");
                animator.Update(0.2f);
                check(continuations == 1 && playback.IsPlaying, "Duplicate Animator Continue is idempotent");
                playback.Complete(new AnimationEvent());
                check(completions == 0 && playback.IsPlaying, "A fabricated Complete cannot terminate a continued visual");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (controller != null) UnityEngine.Object.DestroyImmediate(controller);
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                PresentationClock.Reset();
            }
            return checks;
        }

        // Uses the real Game and full-state codec, without a scene or mocked game model.
        public static int RunPure()
        {
            int checks = 0;
            Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception(name); checks++; };
            var before = new Game { game_uid = "presentation-test", players = new[] { new Player(0), new Player(1) } };
            var player = before.players[0];
            player.hp = player.hp_max = 20;
            player.mana = player.mana_max = 5;
            var unit = new Card("unit", "field", 0) { hp = 4, attack = 2, hp_ongoing = 2 };
            var hand = new Card("unit", "hand", 0) { hp = 3, attack = 1, mana = 4, mana_ongoing = -1 };
            var provider = new Card("unit", "aura", 0) { hp = 1 };
            player.cards_all.Add(unit.uid, unit); player.cards_all.Add(hand.uid, hand); player.cards_all.Add(provider.uid, provider);
            player.cards_board.Add(unit); player.cards_board.Add(provider); player.cards_hand.Add(hand);
            var initial = ReplayStateCodec.Capture(before);
            var after = ReplayStateCodec.Restore(initial);
            after.players[0].hp = 17; after.players[0].mana = 2;
            after.GetCard("field").damage = 2; after.GetCard("field").hp_ongoing = 0;
            after.GetCard("field").attack_ongoing = 3;
            after.GetCard("hand").mana_ongoing = 0; after.GetCard("hand").hp_ongoing = 2;
            after.players[0].cards_board.Remove(after.GetCard("aura"));
            after.players[0].cards_discard.Add(after.GetCard("aura"));
            after.turn_count = 8;
            var final = ReplayStateCodec.Capture(after);
            string initialHash = ReplayStateCodec.Hash(initial), finalHash = ReplayStateCodec.Hash(final);
            var display = new PresentationState();
            int publications = 0;
            display.Changed += () => publications++;
            display.Stage(initial);
            var baselineImpact = display.RequireAnimationEvent();
            display.Seal();
            check(display.Displayed == null && publications == 0, "Initial snapshot also waits for Impact");
            baselineImpact();
            display.Stage(final);
            Action impactA = display.RequireAnimationEvent(), impactB = display.RequireAnimationEvent();
            display.Seal();
            check(ReplayStateCodec.Hash(ReplayStateCodec.Capture(display.Displayed)) == initialHash, "No field/hand/player/zone leak before Impact");
            impactA(); impactA();
            check(display.Pending && publications == 1, "Duplicate/partial marker cannot publish a whole step early");
            impactB();
            check(!display.Pending && publications == 2, "One atomic publication after all linked markers");
            check(ReplayStateCodec.Hash(ReplayStateCodec.Capture(display.Displayed)) == finalHash, "Every serialized field equals the linked result");
            check(display.Displayed.GetCard("hand").mana_ongoing == 0 && display.Displayed.GetCard("hand").hp_ongoing == 2, "Hand mana and stats synchronized");
            check(display.Displayed.GetCard("field").attack_ongoing == 3 && display.Displayed.GetCard("field").hp_ongoing == 0, "Ongoing field stats synchronized");
            check(display.Displayed.players[0].cards_board.Count == 1 && display.Displayed.players[0].cards_discard.Count == 1, "Membership/zone counts synchronized");
            impactB(); check(publications == 2, "No double publication");
            display.Stage(initial); var stale = display.RequireAnimationEvent(); display.Seal();
            display.Cancel();
            display.Stage(final); var reconnectImpact = display.RequireAnimationEvent(); display.Seal(); stale();
            check(display.Pending && publications == 2, "Reset cannot publish and old Impact cannot unlock its replacement");
            reconnectImpact();
            check(ReplayStateCodec.Hash(display.Snapshot) == finalHash && !display.Pending, "Reconnect invalidates stale animation markers");
            display.Stage(initial); display.Seal();
            check(display.Pending && ReplayStateCodec.Hash(display.Snapshot) == finalHash, "No registered event must never publish at Seal");
            display.Seal();
            check(display.Pending, "Repeated Seal cannot replace Impact");
            display.Cancel();
            display.Stage(initial); var early = display.RequireAnimationEvent();
            bool rejectedEarly = false;
            try { early(); } catch (InvalidOperationException) { rejectedEarly = true; }
            check(rejectedEarly && display.Pending, "An unarmed event cannot publish during registration");
            display.Seal(); check(display.Pending, "Seal never replays an early event");
            early(); check(!display.Pending, "Only a new Impact after registration publishes");
            display.Stage(initial); var canceled = display.RequireAnimationEvent(); display.Cancel(); canceled();
            check(ReplayStateCodec.Hash(display.Snapshot) == initialHash, "Cancel never applies an obsolete snapshot");
            check(ReplayStateCodec.Hash(initial) == initialHash && ReplayStateCodec.Hash(final) == finalHash, "Input snapshots unchanged");
            display.Stage(initial);
            var decoration = display.RequireAnimationEvent(0);
            var targetImpact = display.RequireAnimationEvent(2);
            display.Seal(); targetImpact();
            check(!display.Pending && ReplayStateCodec.Hash(display.Snapshot) == initialHash, "Target impact is not delayed by a preview/windup");
            decoration(); check(ReplayStateCodec.Hash(display.Snapshot) == initialHash, "Late decorative event cannot overwrite the result");
            display.Stage(final);
            var immediateDecoration = display.RequireAnimationEvent(0);
            var delayedTarget = display.RequireAnimationEvent(2); // Reserved before prefab creation.
            display.Seal(); immediateDecoration();
            check(display.Pending && ReplayStateCodec.Hash(display.Snapshot) == initialHash, "A delayed target reservation prevents an early decorative Impact from publishing");
            delayedTarget();
            check(!display.Pending && ReplayStateCodec.Hash(display.Snapshot) == finalHash, "Delayed prefab Impact publishes its full linked snapshot");

            display.Stage(initial);
            var boundaryImpact = display.RequireAnimationEvent();
            var boundaryComplete = display.RequireAnimationCompletion();
            display.Seal();
            int floatingCount = 0;
            Action pendingFloating = () => floatingCount++;
            display.Changed += () => { var result = pendingFloating; pendingFloating = null; result?.Invoke(); };
            boundaryImpact();
            check(floatingCount == 1 && !display.Pending, "Impact updates stats and floating result together");
            bool rejectedBoundary = false;
            try { display.SynchronizeEffectBoundary(); } catch (InvalidOperationException) { rejectedBoundary = true; }
            check(rejectedBoundary, "Cannot cross a boundary before the animation completes");
            display.Displayed.players[0].hp = -99; // Prove the boundary restores the exact snapshot.
            boundaryComplete();
            display.SynchronizeEffectBoundary();
            check(ReplayStateCodec.Hash(ReplayStateCodec.Capture(display.Displayed)) == initialHash,
                "Effect boundary forcibly synchronizes every numeric field");
            check(floatingCount == 1, "Boundary synchronization never repeats Impact floating text");
            int boundaryPublications = publications;
            display.SynchronizeEffectBoundary(); boundaryImpact();
            check(publications == boundaryPublications, "Duplicate boundary and late Impact are idempotent");

            display.Stage(final);
            var missingImpact = display.RequireAnimationEvent();
            var completedWithoutImpact = display.RequireAnimationCompletion();
            var delayedCompletion = display.RequireAnimationCompletion();
            display.Seal(); pendingFloating = () => floatingCount++;
            completedWithoutImpact();
            check(!display.CanFinishEffect && display.Pending, "Outstanding delayed FX prevent boundary synchronization");
            delayedCompletion();
            check(display.CanFinishEffect && display.Pending && floatingCount == 1,
                "Complete only marks readiness, never fabricates Impact");
            display.SynchronizeEffectBoundary();
            check(!display.Pending && floatingCount == 2
                && ReplayStateCodec.Hash(ReplayStateCodec.Capture(display.Displayed)) == finalHash,
                "Normal effect boundary reconciles a missing Impact and flushes its result exactly once");
            missingImpact();
            check(floatingCount == 2, "Late Impact cannot replay a boundary-flushed result");
            display.Stage(initial);
            var obsoleteComplete = display.RequireAnimationCompletion();
            display.Seal(); display.Cancel();
            display.Stage(initial); display.RequireAnimationEvent(); display.RequireAnimationCompletion(); display.Seal();
            obsoleteComplete();
            check(!display.CanFinishEffect, "An old completion cannot finish a replacement effect");
            display.Cancel();
            var recorder = new ReplayRecorder();
            recorder.Begin(before, "validation");
            recorder.Event(after, new byte[] { 1 });
            after.players[0].hp++; // An additional effect result after the damage notification.
            recorder.Event(after, new byte[] { 2 });
            recorder.PresentationBoundary(after);
            var entries = recorder.Record.events;
            check(entries.Count == 5 && entries[0].kind == "state" && entries[1].kind == "event"
                && entries[2].kind == "state" && entries[3].kind == "event" && entries[4].kind == "presentation",
                "Replay keeps intermediate damage and target events in one presentation step");
            recorder.PresentationBoundary(after);
            check(entries.Count == 5, "No duplicate replay boundary without an outcome");
            var finished = recorder.Finish(after);
            check(finished.finalHash == ReplayStateCodec.Hash(ReplayStateCodec.Capture(after)), "Replay boundary preserves final state hash");
            checks += RunContinue(initial, final);
            return checks;
        }

        private static int RunContinue(ReplayState initial, ReplayState final)
        {
            int checks = 0;
            Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception(name); checks++; };
            var display = new PresentationState();
            display.Stage(initial);
            var impact = display.RequireAnimationEvent();
            var complete = display.RequireAnimationCompletion(out Action proceed);
            display.Seal(); impact();
            check(!display.CanAdvanceEffect && !display.HasContinuation, "Without Continue an impacted FX still waits for completion");
            complete();
            check(display.CanAdvanceEffect && display.CanFinishEffect && !display.HasContinuation,
                "Existing clips retain their full-completion progression path");
            display.SynchronizeEffectBoundary();

            display.Stage(final);
            impact = display.RequireAnimationEvent();
            complete = display.RequireAnimationCompletion(out proceed);
            display.Seal();
            proceed();
            check(display.Pending && !display.CanAdvanceEffect, "Continue cannot publish or advance past an outstanding Impact");
            impact();
            check(display.CanAdvanceEffect && !display.CanFinishEffect, "Continue after Impact releases pacing without completing the FX");
            proceed();
            display.SynchronizeEffectBoundary();
            check(ReplayStateCodec.Hash(display.Snapshot) == ReplayStateCodec.Hash(final), "Continue boundary preserves the confirmed result");

            // Previous tails may finish after the next snapshot has already been staged.
            var oldImpact = impact;
            var oldComplete = complete;
            var oldContinue = proceed;
            display.Stage(initial);
            var nextImpact = display.RequireAnimationEvent();
            var firstComplete = display.RequireAnimationCompletion(out Action firstContinue);
            var secondComplete = display.RequireAnimationCompletion(out Action secondContinue);
            display.Seal();
            oldImpact(); oldContinue(); oldComplete();
            check(display.Pending && !display.CanAdvanceEffect && !display.HasContinuation,
                "Old tail events cannot publish or release the following step");
            nextImpact(); firstContinue();
            check(!display.CanAdvanceEffect, "One Continue cannot bypass another FX in the same step");
            secondContinue();
            check(display.CanAdvanceEffect && !display.CanFinishEffect, "Every FX may release through Continue while all visuals remain alive");
            firstComplete(); secondComplete();
            check(display.CanFinishEffect, "Continued visuals may still complete and clean up later");
            display.SynchronizeEffectBoundary();

            display.Stage(final);
            impact = display.RequireAnimationEvent();
            firstComplete = display.RequireAnimationCompletion(out firstContinue);
            secondComplete = display.RequireAnimationCompletion();
            display.Seal(); impact(); firstContinue(); secondComplete();
            check(display.CanAdvanceEffect && !display.CanFinishEffect, "Continue and full completion may release different FX in one step");
            display.SynchronizeEffectBoundary();

            display.Stage(initial);
            display.RequireAnimationEvent();
            complete = display.RequireAnimationCompletion(out proceed);
            proceed();
            check(!display.HasContinuation && !display.CanAdvanceEffect, "Unarmed Continue during registration has no effect");
            display.Seal(); display.Cancel(); proceed(); complete();
            check(!display.CanAdvanceEffect && !display.HasContinuation, "Reset invalidates Continue and completion together");
            return checks;
        }
    }
}
