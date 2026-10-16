using System;
using System.Collections.Generic;
using TcgEngine.Replay;

namespace TcgEngine.Client
{
    // The only writer of the displayed game. Animation callbacks carry a generation,
    // never a value to add/subtract. All fields/zones change atomically from one snapshot.
    public sealed class PresentationState
    {
        public Game Displayed { get; private set; }
        public ReplayState Snapshot { get; private set; }
        public bool Pending => staged != null;
        public bool HasAnimationRequests => nextMarker != 0;
        internal long Generation => generation;
        public event Action Changed;
        private ReplayState staged;
        private long generation;
        private bool sealedStep;
        private readonly HashSet<int> markers = new HashSet<int>();
        private int nextMarker;
        private int highestPriority;
        private readonly HashSet<int> completions = new HashSet<int>();
        private readonly HashSet<int> progression = new HashSet<int>();
        private int nextCompletion;
        private bool boundarySynchronized;
        public bool HasContinuation { get; private set; }
        public bool CanFinishEffect => sealedStep && nextCompletion > 0 && completions.Count == 0;
        // Continue releases pacing, not results. Every FX must release its gate, and
        // the linked snapshot must already be published before overlapping another step.
        public bool CanAdvanceEffect => CanFinishEffect
            || (sealedStep && nextCompletion > 0 && progression.Count == 0 && !Pending);

        public void Stage(ReplayState state)
        {
            if (Pending) throw new InvalidOperationException("Previous presentation state is still pending");
            generation++;
            staged = ReplayStateCodec.Copy(state);
            sealedStep = false;
            markers.Clear();
            nextMarker = 0;
            highestPriority = int.MinValue;
            completions.Clear();
            progression.Clear();
            nextCompletion = 0;
            boundarySynchronized = false;
            HasContinuation = false;
        }

        // Completion is reserved before spawning, just like Impact. Delayed, missing,
        // canceled or failed FX cannot be mistaken for a successfully finished effect.
        public Action RequireAnimationCompletion()
        {
            return RequireAnimationCompletion(out _);
        }

        public Action RequireAnimationCompletion(out Action onContinue)
        {
            if (!Pending || sealedStep) throw new InvalidOperationException("Completion must be reserved before sealing");
            long version = generation;
            int id = ++nextCompletion;
            completions.Add(id);
            progression.Add(id);
            onContinue = () => {
                if (version != generation || !sealedStep || !progression.Remove(id)) return;
                HasContinuation = true;
            };
            return () => {
                if (version != generation || !sealedStep) return;
                completions.Remove(id);
                progression.Remove(id);
            };
        }

        public Action RequireAnimationEvent(int priority = 0)
        {
            if (!Pending || sealedStep) throw new InvalidOperationException("Animation must bind before sealing a presentation step");
            // A target hit/entry/death owns the result. Decorative previews and caster
            // windups must not postpone it when a more precise impact cue is available.
            if (priority < highestPriority) return () => { };
            if (priority > highestPriority) { highestPriority = priority; markers.Clear(); }
            long version = generation;
            int marker = ++nextMarker;
            markers.Add(marker);
            return () => {
                if (version == generation && !sealedStep)
                    throw new InvalidOperationException("Impact arrived before animation registration closed");
                if (version != generation || !markers.Remove(marker)) return;
                PublishFromImpact();
            };
        }

        // Closing registration never publishes, even when no animations were registered.
        public void Seal() { sealedStep = true; }

        private void PublishFromImpact()
        {
            if (!sealedStep || !Pending || markers.Count != 0) return;
            Publish(staged);
        }

        // The second synchronization point: after every FX completed or allowed Continue.
        // Early handoff requires Impact; only full completion retains missing-Impact recovery.
        public void SynchronizeEffectBoundary()
        {
            if (!CanAdvanceEffect) throw new InvalidOperationException("Effect animations have not allowed progression");
            if (boundarySynchronized) return;
            boundarySynchronized = true;
            markers.Clear();
            Publish(staged ?? Snapshot);
        }

        private void Publish(ReplayState state)
        {
            staged = null;
            Snapshot = state;
            Displayed = ReplayStateCodec.Restore(state);
            Changed?.Invoke();
        }

        public void Cancel()
        {
            generation++;
            staged = null;
            markers.Clear();
            completions.Clear();
            progression.Clear();
            nextCompletion = 0;
            sealedStep = false;
            HasContinuation = false;
        }
    }
}
