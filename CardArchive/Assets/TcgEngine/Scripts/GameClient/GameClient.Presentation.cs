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
        private ReplayState latestState;
        private ReplayState displayedState;
        private readonly Queue<PresentationFrame> presentationQueue = new Queue<PresentationFrame>();
        private readonly HashSet<string> pendingCards = new HashSet<string>();
        private readonly StringBuilder incomingBatch = new StringBuilder();
        private long receivedSequence;
        private long displayedSequence;
        private long inputEpoch;
        private long barrierSequence = -1;
        private long acknowledgedBarrier = -1;
        private bool inputClosed = true;
        private bool presentationReady;
        private bool playingPresentation;
        private Coroutine presentationRoutine;
        private int nextChunk;
        private float timerValue;
        private float timerReceivedAt;
        public float TurnTimeRemaining => Mathf.Max(0f, timerValue - (inputClosed || latest_data == null
            || (latest_data.phase != GamePhase.Main && latest_data.phase != GamePhase.Mulligan) ? 0f : Time.time - timerReceivedAt));
        private void SynchronizeTimer() { timerValue = latest_data.turn_timer; timerReceivedAt = Time.time; }

        public Game GetLatestGameData() => latest_data ?? game_data;
        public bool IsInputOpen => presentationReady && !inputClosed && !observe_mode && IsReady();
        public bool IsCardPending(string uid) => pendingCards.Contains(uid);
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
                if (presentationRoutine != null) StopCoroutine(presentationRoutine);
                presentationRoutine = null;
                playingPresentation = false;
                PresentationClock.Reset();
                presentationQueue.Clear();
                pendingCards.Clear();
                latestState = JsonUtility.FromJson<ReplayState>(json);
                displayedState = ReplayStateCodec.Copy(latestState);
                latest_data = ReplayStateCodec.Restore(latestState);
                SynchronizeTimer();
                game_data = ReplayStateCodec.Restore(displayedState);
                receivedSequence = displayedSequence = msg.sequence;
                presentationReady = true;
                onRefreshAll?.Invoke();
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
            SynchronizeTimer();
            if (presentationRoutine == null && presentationQueue.Count > 0)
                presentationRoutine = StartCoroutine(PlayPresentation());
        }

        private IEnumerator PlayPresentation()
        {
            playingPresentation = true;
            PresentationClock.Active = true;
            while (presentationQueue.Count > 0)
            {
                var group = PresentationPlayback.DequeueGroup(presentationQueue);
                PresentationClock.Begin();
                foreach (var frame in group)
                    displayedState = ReplayStateCodec.Apply(displayedState, frame.changes);
                game_data = ReplayStateCodec.Restore(displayedState);
                onRefreshAll?.Invoke();
                // Board/hand objects poll the displayed model; let them spawn/remove before FX dispatch.
                yield return null;
                if (group.Count > 1)
                {
                    // All outcomes in a group are normal draws from the same request.
                    // Refresh once to spawn the cards together and play their draw cue once.
                    onCardDraw?.Invoke(group.Count);
                    PresentationClock.Hold(EventMinimum(GameAction.CardDrawn));
                }
                else foreach (string packet in group[0].events)
                {
                    byte[] bytes = Convert.FromBase64String(packet);
                    using (var reader = new FastBufferReader(bytes, Allocator.Temp))
                    {
                        reader.ReadValueSafe(out ushort type);
                        if (registered_commands.TryGetValue(type, out var command))
                            command.callback.Invoke(new SerializedData(reader));
                        PresentationClock.Hold(EventMinimum(type));
                    }
                }
                // Include FX started by Update/Start and the final spacing in completion.
                yield return null;
                while (!PresentationClock.Complete) yield return null;
                displayedSequence = group[group.Count - 1].sequence;
                TryAcknowledgePresentation();
            }
            playingPresentation = false;
            PresentationClock.Active = false;
            presentationRoutine = null;
            TryAcknowledgePresentation();
        }

        private static float EventMinimum(ushort type)
        {
            switch (type)
            {
                case GameAction.CardDrawn: return 0.55f;
                case GameAction.CardPlayed:
                case GameAction.CardSummoned:
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
            timerValue = Mathf.Max(0f, msg.timer);
            timerReceivedAt = Time.time;
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
            pendingCards.Remove(data.Get<MsgCard>().card_uid);
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
        public static void Begin() { until = Time.time + Gap; }
        public static void Hold(float duration)
        {
            if (Active) until = Mathf.Max(until, Time.time + Mathf.Max(0f, duration) + Gap);
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
        public static void Reset() { Active = false; operations.Clear(); until = 0f; }
        public static bool Complete
        {
            get
            {
                for (int i = operations.Count - 1; i >= 0; i--)
                    if (operations[i]())
                    {
                        operations.RemoveAt(i);
                        until = Mathf.Max(until, Time.time + Gap);
                    }
                if (operations.Count > 0) { Hold(0f); return false; }
                return Time.time >= until;
            }
        }
    }
}
