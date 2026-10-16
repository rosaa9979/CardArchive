using System;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEditor;
using UnityEngine;

namespace TcgEngine.Editor
{
    public static class OngoingSpawnValidation
    {
        [MenuItem("TcgEngine/FX/Validate Ongoing Spawn (Play Mode)")]
        public static void Validate() => Debug.Log("Ongoing spawn checks passed: " + Run());

        public static int Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            int checks = 0;
            Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); checks++; };
            var gates = new OngoingSpawnPresentation();
            var card = new Card("unit", "ongoing-spawn", 0) { play_order = 1 };
            check(gates.CanShow(card), "Baseline cards have no artificial spawn gate");
            var stale = gates.Begin(card);
            check(!gates.CanShow(card), "New spawn waits for its marker");
            var release = gates.Begin(card); stale();
            check(!gates.CanShow(card), "An earlier spawn cannot release its replacement");
            release(); release();
            check(gates.CanShow(card), "Spawn marker is idempotent");
            stale = gates.Begin(card); gates.Clear(); release = gates.Begin(card); stale();
            check(!gates.CanShow(card), "Reset invalidates callbacks from the previous presentation");
            release();
            var previous = gates.Begin(card); card.play_order = 2; release = gates.Begin(card); previous();
            check(!gates.CanShow(card), "Re-entry of the same UID has an independent spawn gate");
            release();
            var other = new Card("unit", "other-spawn", 0);
            release = gates.Begin(card); var releaseOther = gates.Begin(other); release();
            check(gates.CanShow(card) && !gates.CanShow(other), "Simultaneous spawns do not release each other");
            releaseOther();

            int impacts = 0, starts = 0, completions = 0;
            using (var cue = new Cue(new[] { Marker(.1f, "Impact"), Marker(.5f, "StartOngoing"), Marker(.9f, "Complete") }))
            {
                release = gates.Begin(card);
                cue.Playback.Bind(_ => impacts++, () => completions++);
                cue.Playback.BindOngoingStart(() => { starts++; release(); });
                cue.Playback.StartOngoing(new AnimationEvent());
                check(starts == 0 && impacts == 0, "Fabricated or unarmed StartOngoing is ignored");
                check(!cue.Animator.enabled, "Spawn callbacks bind before Animator activation");
                cue.Playback.Arm(); cue.Animator.Update(0f); cue.Animator.Update(.2f);
                check(impacts == 1 && starts == 0 && !gates.CanShow(card), "Impact publishes card before ongoing starts");
                cue.Animator.Update(.4f);
                check(starts == 1 && completions == 0 && gates.CanShow(card), "Authored marker enables ongoing while spawn tail continues");
                cue.Animator.Update(.4f);
                check(starts == 1 && completions == 1, "Complete does not repeat an explicit ongoing marker");
            }
            starts = impacts = completions = 0;
            using (var cue = new Cue(new[] { Marker(.1f, "Impact"), Marker(.9f, "Complete") }))
            {
                release = gates.Begin(card);
                cue.Playback.Bind(_ => impacts++, () => completions++);
                cue.Playback.BindOngoingStart(() => { starts++; release(); });
                cue.Playback.Arm(); cue.Animator.Update(0f); cue.Animator.Update(.5f);
                check(impacts == 1 && starts == 0, "Without a marker, ongoing waits through spawn");
                cue.Animator.Update(.5f);
                check(starts == 1 && completions == 1 && gates.CanShow(card), "Complete is the existing-asset default start boundary");
            }
            var order = new List<string>();
            using (var cue = new Cue(new[] { Marker(.4f, "Complete"), Marker(.4f, "Continue"), Marker(.4f, "StartOngoing"), Marker(.4f, "Impact") }))
            {
                cue.Playback.Bind(_ => order.Add("Impact"), () => order.Add("Complete"), () => order.Add("Continue"));
                cue.Playback.BindOngoingStart(() => order.Add("StartOngoing"));
                cue.Playback.Arm(); cue.Animator.Update(0f); cue.Animator.Update(.5f);
                check(string.Join(",", order) == "Impact,StartOngoing,Continue,Complete", "Same-frame events retain result and progression ordering");
            }
            starts = impacts = completions = 0;
            using (var cue = new Cue(new[] { Marker(.1f, "StartOngoing"), Marker(.5f, "Impact"), Marker(.9f, "Complete") }))
            {
                cue.Playback.Bind(_ => impacts++, () => completions++);
                cue.Playback.BindOngoingStart(() => starts++);
                cue.Playback.Arm(); cue.Animator.Update(0f); cue.Animator.Update(.2f);
                check(starts == 1 && impacts == 0 && completions == 0, "StartOngoing cannot fabricate Impact or Complete");
                cue.Playback.Cancel();
                check(impacts == 0 && completions == 0 && starts == 1, "Cancellation does not apply results or duplicate the marker");
            }
            starts = 0;
            using (var cue = new Cue(new[] { Marker(0f, "StartOngoing"), Marker(.9f, "Complete") }))
            {
                cue.Playback.Bind(Array.Empty<Action>(), null);
                cue.Playback.BindOngoingStart(() => starts++);
                check(starts == 0, "First-frame marker waits for registration");
                cue.Playback.Arm(); cue.Animator.Update(0f); cue.Animator.Update(.01f);
                check(starts == 1, "First-frame marker is delivered after arming");
            }
            starts = 0;
            using (var cue = new Cue(new[] { Marker(.8f, "StartOngoing"), Marker(.9f, "Complete") }))
            {
                release = gates.Begin(card);
                cue.Playback.Bind(Array.Empty<Action>(), null);
                cue.Playback.BindOngoingStart(() => { starts++; release(); });
                cue.Playback.Arm(); gates.Clear(); var replacement = gates.Begin(card); cue.Playback.Cancel();
                check(starts == 1 && !gates.CanShow(card), "Cancel cleanup cannot open a new-generation spawn");
                replacement();
            }
            return checks;
        }

        private static AnimationEvent Marker(float time, string name) => new AnimationEvent { time = time, functionName = name };

        private sealed class Cue : IDisposable
        {
            private readonly GameObject root;
            private readonly AnimationClip clip;
            private readonly AnimatorOverrideController controller;
            public readonly FX.FXPlayback Playback;
            public readonly Animator Animator;

            public Cue(AnimationEvent[] events)
            {
                var template = Resources.Load<RuntimeAnimatorController>("Presentation/StateCue");
                clip = UnityEngine.Object.Instantiate(template.animationClips[0]);
                clip.SetCurve("", typeof(FX.FXPlayback), "cueProgress", AnimationCurve.Linear(0f, 0f, 1f, 1f));
                clip.events = events;
                controller = new AnimatorOverrideController(template);
                controller[template.animationClips[0].name] = clip;
                root = new GameObject("Ongoing spawn validation") { hideFlags = HideFlags.HideAndDontSave };
                Animator = root.AddComponent<Animator>(); Animator.runtimeAnimatorController = controller;
                Playback = FX.FXPlayback.Prepare(root, false);
            }
            public void Dispose()
            {
                Playback.Cancel();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(controller);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }
    }
}
