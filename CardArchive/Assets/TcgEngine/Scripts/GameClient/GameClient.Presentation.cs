using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TcgEngine.Replay;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TcgEngine.Client
{
    public partial class GameClient
    {
        private Game latest_data;
        private readonly PresentationState presentationState = new PresentationState();
        private Game eventState;
        private Game EventData => eventState ?? game_data;
        private bool collectingAnimations;
        private bool abilityImpactReserved;
        private int presentationCuePriority;
        private readonly List<TcgEngine.FX.FXPlayback> presentationFX = new List<TcgEngine.FX.FXPlayback>();
        private readonly List<TcgEngine.FX.FXPlayback> continuingFX = new List<TcgEngine.FX.FXPlayback>();
        private readonly List<Action> afterPresentationCommit = new List<Action>();
        public bool IsPresentingState => playingPresentation;
        private ReplayState latestState;
        private readonly Queue<PresentationFrame> presentationQueue = new Queue<PresentationFrame>();
        private readonly CardPlayFeedback cardPlays = new CardPlayFeedback();
        // Displayed plays only. A later re-entry of the same UID has a different play_order.
        private readonly Dictionary<string, int> directCardPlays = new Dictionary<string, int>();
        private readonly StringBuilder incomingBatch = new StringBuilder();
        private long receivedSequence;
        private long displayedSequence;
        private long inputEpoch;
        private long barrierSequence = -1;
        private long acknowledgedBarrier = -1;
        private bool inputClosed = true;
        private bool presentationReady;
        private bool connectNotificationPending;
        private bool playingPresentation;
        private Coroutine presentationRoutine;
        private int nextChunk;
        // The displayed timer is part of the snapshot too; UI cannot extrapolate new values.
        public float TurnTimeRemaining => game_data != null ? game_data.turn_timer : 0f;

        public Game GetLatestGameData() => latest_data ?? game_data;
        // Display publication waits for Impact independently of the input gate.
        public bool IsInputOpen => presentationReady && !inputClosed && !observe_mode && IsReady();
        public bool IsCardPending(string uid) => cardPlays.IsPending(uid);
        public bool IsCardAwaitingResult(string uid) => cardPlays.IsAwaitingResult(uid);
        public bool IsHandCardSuppressed(string uid) => cardPlays.IsAccepted(uid);
        public bool IsDirectCardPlay(Card card) => card != null
            && directCardPlays.TryGetValue(card.uid, out int order) && order == card.play_order;
        public bool CanUseBoardCard(string uid)
        {
            Game latest = GetLatestGameData();
            Card card = latest?.GetCard(uid);
            return IsInputOpen && latest != null && latest.CanInputTarget(card);
        }

        private void OnPresentation(SerializedData data)
        {
            var msg = data.Get<MsgPresentation>();
            if (msg.part == 0) { incomingBatch.Clear(); nextChunk = 0; }
            if (msg.part != nextChunk++) { RequestPresentationSync(); return; }
            incomingBatch.Append(msg.json);
            if (msg.part + 1 < msg.parts) return;
            string json = incomingBatch.ToString();
            incomingBatch.Clear();
            if (msg.reset)
            {
                CancelPresentationStep();
                if (presentationRoutine != null) StopCoroutine(presentationRoutine);
                presentationRoutine = null;
                playingPresentation = false;
                PresentationClock.Reset();
                presentationQueue.Clear();
                cardPlays.Clear();
                directCardPlays.Clear();
                latestState = JsonUtility.FromJson<ReplayState>(json);
                latest_data = ReplayStateCodec.Restore(latestState);
                receivedSequence = msg.sequence;
                presentationReady = true;
                ResetPresentation(latestState, msg.sequence);
                return;
            }
            if (!presentationReady) return; // Initial handshake will supply a matching baseline.
            var batch = JsonUtility.FromJson<PresentationBatch>(json);
            foreach (var frame in batch.frames)
            {
                if (frame.sequence <= receivedSequence) continue;
                if (frame.sequence != receivedSequence + 1) { RequestPresentationSync(); return; }
                latestState = ReplayStateCodec.Apply(latestState, frame.changes);
                receivedSequence = frame.sequence;
                presentationQueue.Enqueue(frame);
            }
            latest_data = ReplayStateCodec.Restore(latestState);
            if (presentationRoutine == null && presentationQueue.Count > 0)
                presentationRoutine = StartCoroutine(PlayPresentation());
        }

        private IEnumerator PlayPresentation(ReplayState baseline = null, long baselineSequence = 0)
        {
            playingPresentation = true;
            PresentationClock.Active = true;
            if (baseline != null)
            {
                yield return PresentState(baseline, new List<string>(), baselineSequence, baseline: true);
                displayedSequence = baselineSequence;
            }
            while (presentationQueue.Count > 0)
            {
                var group = PresentationPlayback.DequeueGroup(presentationQueue);
                ReplayState next = presentationState.Snapshot;
                foreach (var frame in group) next = ReplayStateCodec.Apply(next, frame.changes);
                var packets = new List<string>();
                foreach (var frame in group) packets.AddRange(frame.events);
                yield return PresentState(next, packets, group[group.Count - 1].sequence, group.Count > 1);
                displayedSequence = group[group.Count - 1].sequence;
                TryAcknowledgePresentation();
            }
            playingPresentation = false;
            PresentationClock.Active = false;
            presentationRoutine = null;
            TryAcknowledgePresentation();
        }

        // Live and replay both enter here. Pending event payloads may reference future cards,
        // but GetGameData/GetPlayer and every numeric UI see only the published snapshot.
        private IEnumerator PresentState(ReplayState next, List<string> packets, long sequence, bool groupedDraw = false, bool baseline = false)
        {
            PresentationClock.Begin();
            presentationState.Stage(next);
            abilityImpactReserved = false;
            eventState = ReplayStateCodec.Restore(next);
            collectingAnimations = true;
            if (!baseline) PrepareBoardTransitions(eventState);
            bool drawQueued = false;
            foreach (string packet in packets)
            {
                byte[] bytes = Convert.FromBase64String(packet);
                using (var reader = new FastBufferReader(bytes, Allocator.Temp))
                {
                    reader.ReadValueSafe(out ushort type);
                    if (groupedDraw && type == GameAction.CardDrawn)
                    {
                        if (drawQueued) continue;
                        drawQueued = true;
                        AfterPresentationCommit(() => onCardDraw?.Invoke(packets.Count));
                    }
                    else if (registered_commands.TryGetValue(type, out var command))
                    command.callback.Invoke(new SerializedData(reader));
                    PresentationClock.HoldEventMinimum(EventMinimum(type));
                }
            }
            AfterPresentationCommit(() => {
                Player hand = game_data.GetPlayer(GetPlayerID());
                if (hand != null) cardPlays.ObserveDisplayedHand(hand, sequence);
            });
            // A step with no visual FX still has an explicit Animator timeline. This is
            // never a fallback for a missing/broken/delayed FX: those already reserved a cue.
            if (!presentationState.HasAnimationRequests) CreateStateCue();
            presentationState.Seal();
            collectingAnimations = false;
            foreach (var fx in presentationFX) if (fx != null) fx.Arm();
            while (true)
            {
                if (presentationState.CanAdvanceEffect)
                {
                    if (presentationState.HasContinuation) PresentationClock.ContinueCurrentStep();
                    if (PresentationClock.Complete) break;
                }
                yield return null;
            }
            // Normal Impact already flushed result callbacks. At the effect boundary
            // reapply the exact final snapshot; any still-pending results flush once here.
            presentationState.SynchronizeEffectBoundary();
            // Let scene objects created by publication finish Start/FX before the next state.
            yield return null;
            while (!PresentationClock.Complete) yield return null;
            eventState = null;
            continuingFX.RemoveAll(fx => fx == null || !fx.IsPlaying);
            foreach (var fx in presentationFX)
                if (fx != null && fx.IsPlaying) continuingFX.Add(fx);
            presentationFX.Clear();
        }

        private void ResetPresentation(ReplayState state, long sequence = 0)
        {
            CancelPresentationStep();
            if (presentationRoutine != null) StopCoroutine(presentationRoutine);
            PresentationClock.Reset();
            presentationQueue.Clear();
            playingPresentation = true;
            presentationRoutine = StartCoroutine(PlayPresentation(state, sequence));
        }

        private void CancelPresentationStep()
        {
            presentationState.Cancel();
            collectingAnimations = false;
            afterPresentationCommit.Clear();
            foreach (var fx in presentationFX) if (fx != null) fx.Cancel();
            foreach (var fx in continuingFX) if (fx != null) fx.Cancel();
            presentationFX.Clear();
            continuingFX.Clear();
            eventState = null;
            preparedSpawns.Clear();
            preparedDeaths.Clear();
        }

        private void OnPresentationStateChanged()
        {
            // Numeric views refresh in the same render frame, before result floating text.
            if (observe_mode && !ReplaySession.Active) SetObserverMode(observe_user);
            GameBoard.Get()?.RefreshDisplayedState();
            HandCardArea.Get()?.RefreshCards();
            onRefreshAll?.Invoke();
            var callbacks = afterPresentationCommit.ToArray();
            afterPresentationCommit.Clear();
            foreach (var callback in callbacks) callback();
            if (connectNotificationPending)
            {
                connectNotificationPending = false;
                onConnectGame?.Invoke();
            }
        }

        private void AfterPresentationCommit(Action action)
        {
            if (presentationState.Pending) afterPresentationCommit.Add(action);
            else action();
        }

        // Reserve before creating OR scheduling a prefab. The generation survives a
        // delayed spawn but is invalidated by reset, cancellation or the next step.
        public sealed class AnimationReservation
        {
            internal long generation;
            internal Action impact;
            internal Action complete;
            internal Action continuation;
            internal bool bound;
        }

        public AnimationReservation ReservePresentationAnimation(GameObject prefab = null, bool abilityTarget = false)
        {
            if (!presentationState.Pending) return null; // Post-commit decorative FX.
            if (!collectingAnimations)
                throw new InvalidOperationException("Late FX must be reserved with ScheduleStateFX before delaying their creation");
            var setting = prefab != null ? prefab.GetComponentInChildren<TcgEngine.FX.FXSetting>(true) : null;
            bool appliesState = setting == null || setting.presentation_impact;
            int priority = presentationCuePriority;
            if (abilityTarget && appliesState)
            {
                // Packet/target order selects the owner before any Animator is armed.
                // Other instances keep their own animation and completion, but cannot publish.
                appliesState = !abilityImpactReserved;
                abilityImpactReserved = true;
                priority = 3; // The ability result wins over nested spawn/death or board cues.
            }
            Action complete = presentationState.RequireAnimationCompletion(out Action continuation);
            return new AnimationReservation {
                generation = presentationState.Generation,
                impact = appliesState ? presentationState.RequireAnimationEvent(priority) : null,
                complete = complete,
                continuation = continuation
            };
        }

        public bool IsCurrentReservation(AnimationReservation reservation) => reservation == null
            || reservation.generation == presentationState.Generation;

        public bool BindPresentationAnimation(GameObject root, AnimationReservation reservation)
        {
            if (reservation == null) return false;
            if (!IsCurrentReservation(reservation)) { Destroy(root); return true; }
            if (reservation.bound) throw new InvalidOperationException("Animation reservation already bound");
            reservation.bound = true;
            var playback = TcgEngine.FX.FXPlayback.Prepare(root, reservation.impact != null);
            if (playback == null)
            {
                Debug.LogError("Presentation FX has no usable animation timeline; state remains pending: " + root.name, root);
                Destroy(root);
                return true;
            }
            playback.Bind(reservation.impact != null ? new[] { reservation.impact } : Array.Empty<Action>(),
                () => {
                    reservation.complete();
                    continuingFX.Remove(playback);
                }, reservation.continuation);
            presentationFX.Add(playback);
            if (!collectingAnimations) playback.Arm();
            return true;
        }

        private void CreateStateCue()
        {
            var reservation = ReservePresentationAnimation();
            var root = new GameObject("Presentation State Cue");
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Presentation/StateCue");
            animator.speed = 60f; // Short neutral clip, still dispatched by the real Animator.
            BindPresentationAnimation(root, reservation);
        }

        public IDisposable PresentationCue(int priority)
        {
            int previous = presentationCuePriority;
            presentationCuePriority = priority;
            return new CueScope(() => presentationCuePriority = previous);
        }

        private sealed class CueScope : IDisposable
        {
            private readonly Action restore;
            public CueScope(Action restore) { this.restore = restore; }
            public void Dispose() { restore(); }
        }

        public IEnumerator PresentReplayState(ReplayState next, List<string> packets)
        {
            if (!ReplaySession.Active) throw new InvalidOperationException();
            while (playingPresentation) yield return null;
            playingPresentation = true;
            PresentationClock.Active = true;
            yield return PresentState(next, packets, 0);
            playingPresentation = false;
            PresentationClock.Active = false;
        }

        private static float EventMinimum(ushort type)
        {
            switch (type)
            {
                case GameAction.CardDrawn: return 0.55f;
                case GameAction.CardPlayed:
                case GameAction.CardSummoned: return 0f;
                case GameAction.CardDiscarded: return 0.7f;
                case GameAction.AttackStart:
                case GameAction.AttackPlayerStart: return 0.35f;
                case GameAction.AttackEnd:
                case GameAction.AttackPlayerEnd: return 0.65f;
                default: return 0f;
            }
        }

        private void OnPresentationGate(SerializedData data)
        {
            var msg = data.Get<MsgPresentation>();
            if (msg.barrier < inputEpoch) return;
            inputEpoch = msg.barrier;
            inputClosed = msg.closed;
            if (inputClosed)
            {
                HandCard.CancelPress();
                HandCard dragged = HandCard.GetDrag();
                if (dragged != null) dragged.SetDrag(false);
            }
            barrierSequence = msg.sequence;
            TryAcknowledgePresentation();
        }

        private void TryAcknowledgePresentation()
        {
            if (presentationState.Pending) return;
            if (playingPresentation || presentationQueue.Count != 0 || !PresentationClock.Complete) return;
            if (!presentationReady || observe_mode || barrierSequence < 0 || displayedSequence < barrierSequence
                || receivedSequence < barrierSequence || acknowledgedBarrier == inputEpoch) return;
            acknowledgedBarrier = inputEpoch;
            SendAction(GameAction.PresentationAck, new MsgPresentation { barrier = inputEpoch, sequence = barrierSequence });
        }

        private void RequestPresentationSync()
        {
            inputClosed = true;
            SendAction(GameAction.PresentationSync, new MsgPresentation { sequence = receivedSequence, reset = !presentationReady });
        }

        private void OnActionResult(SerializedData data)
        {
            if (cardPlays.Resolve(data.Get<MsgCardActionResult>()))
                HandCardArea.Get()?.RefreshCards();
        }

        private bool CanSendAction(ushort type)
        {
            if (type == GameAction.PresentationAck || type == GameAction.PresentationSync || type == GameAction.Resign
                || type == GameAction.ChatMessage || type >= GameAction.PlayerSettings && type <= GameAction.GameSettings) return true;
            return IsInputOpen;
        }
    }

    // Actual FX producers extend the current frame. Only the client knows animation duration.
    public static class PresentationClock
    {
        public const float Gap = 0.15f;
        public static bool Active { get; set; }
        private static float until;
        private static float eventMinimumUntil;
        private static float stepGap = Gap;
        public static void Begin() { stepGap = Gap; until = Time.time; eventMinimumUntil = Time.time; }
        // Spawn completion should hand off directly to the card's OnPlay presentation.
        public static void SkipGapForCurrentStep()
        {
            stepGap = 0f;
        }
        public static void HoldEventMinimum(float duration)
        {
            if (Active) eventMinimumUntil = Mathf.Max(eventMinimumUntil, Time.time + Mathf.Max(0f, duration));
        }
        public static void ContinueCurrentStep()
        {
            // Explicit Continue bypasses padding/minimum display time, while unrelated
            // tracked operations and real timed callbacks must still finish normally.
            SkipGapForCurrentStep();
            eventMinimumUntil = Time.time;
        }
        public static void Hold(float duration)
        {
            if (Active) until = Mathf.Max(until, Time.time + Mathf.Max(0f, duration));
        }
        private static readonly List<Func<bool>> operations = new List<Func<bool>>();
        public static void Track(Func<bool> complete)
        {
            if (Active || (GameClient.Get() != null && !ReplaySession.Active)) operations.Add(complete);
        }
        public static IDisposable Operation()
        {
            var operation = new PendingOperation();
            Track(() => operation.done);
            return operation;
        }
        private sealed class PendingOperation : IDisposable
        {
            public bool done;
            public void Dispose() { done = true; }
        }
        public static void Reset() { Active = false; operations.Clear(); until = 0f; eventMinimumUntil = 0f; stepGap = 0f; }
        public static bool Complete
        {
            get
            {
                for (int i = operations.Count - 1; i >= 0; i--)
                    if (operations[i]())
                    {
                        operations.RemoveAt(i);
                        until = Mathf.Max(until, Time.time);
                    }
                if (operations.Count > 0) { Hold(0f); return false; }
                return Time.time >= Mathf.Max(until, eventMinimumUntil) + stepGap;
            }
        }
    }
}
