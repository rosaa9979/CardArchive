using System;
using System.Collections.Generic;
using TcgEngine.Replay;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TcgEngine.Server
{
    public partial class GameServer
    {
        private readonly PresentationRecorder presentation = new PresentationRecorder();
        private readonly PresentationBarrier barrier = new PresentationBarrier();
        private Action nextPhase;
        private bool inputClosed;
        private bool endRequested;
        private bool presentationDirty;
        private readonly List<PresentationFrame> presentationHistory = new List<PresentationFrame>();

        private void CapturePresentation(Game game)
        {
            presentation.Capture(game);
            replay.PresentationBoundary(game);
            presentationDirty = false;
        }

        private void FlushPresentation()
        {
            if (presentationDirty) CapturePresentation(game_data);
            var batch = presentation.Take();
            if (batch.frames.Count == 0) return;
            presentationHistory.AddRange(batch.frames);
            SendPresentation(GameAction.Presentation, new MsgPresentation {
                sequence = presentation.Sequence, json = JsonUtility.ToJson(batch) });
        }

        private IEnumerable<int> PresentationParticipants()
        {
            foreach (Player player in game_data.players)
                if (!player.is_ai) yield return player.player_id;
        }

        private void WaitForPresentation(Action continuation)
        {
            if (barrier.Active) throw new InvalidOperationException("Overlapping phase transitions");
            inputClosed = true;
            gameplay.SuspendResolution(true);
            FlushPresentation();
            nextPhase = continuation;
            barrier.Begin(presentation.Sequence, PresentationParticipants());
            SendGate();
        }

        private void UpdatePresentationBarrier()
        {
            if (!barrier.Complete || game_data.HasEnded()) return;
            Action continuation = nextPhase;
            nextPhase = null;
            barrier.Release();
            gameplay.SuspendResolution(false);
            endRequested = false;
            // Keep input closed until the phase entry and all immediately resolvable effects finish.
            continuation();
            gameplay.Update(0f);
            if (!barrier.Active)
            {
                inputClosed = false;
                FlushPresentation();
                SendGate();
            }
        }

        private void ClosePhaseInput()
        {
            if (inputClosed) return;
            inputClosed = true;
            endRequested = true;
            SendGate();
        }

        private void DrainPhaseInput()
        {
            if (!endRequested || barrier.Active || queued_actions.Count != 0 || gameplay.IsResolving()) return;
            endRequested = false;
            if (game_data.phase == GamePhase.Mulligan)
            {
                foreach (Player player in game_data.players)
                    if (!player.ready) gameplay.Mulligan(player, Array.Empty<string>());
            }
            else gameplay.NextStep();
        }

        private void SendGate(ClientData target = null)
        {
            SendPresentation(GameAction.PresentationGate, new MsgPresentation {
                barrier = barrier.Id, sequence = barrier.Active ? barrier.Sequence : -1,
                closed = inputClosed || barrier.Active, timer = game_data.turn_timer }, target);
        }

        private void ReceivePresentationAck(ClientData client, SerializedData data)
        {
            var ack = data.Get<MsgPresentation>();
            Player player = GetPlayer(client);
            if (player != null && !player.is_ai)
                barrier.Acknowledge(player.player_id, ack.barrier, ack.sequence);
        }

        private void ReceivePresentationSync(ClientData client, SerializedData data)
        {
            var request = data.Get<MsgPresentation>();
            FlushPresentation();
            if (!request.reset)
            {
                var missed = new PresentationBatch();
                foreach (var frame in presentationHistory)
                    if (frame.sequence > request.sequence) missed.frames.Add(frame);
                SendPresentation(GameAction.Presentation, new MsgPresentation {
                    sequence = presentation.Sequence, json = JsonUtility.ToJson(missed) }, client);
                SendGate(client);
                return;
            }
            SendPresentation(GameAction.Presentation, new MsgPresentation {
                reset = true, sequence = presentation.Sequence,
                json = JsonUtility.ToJson(ReplayStateCodec.Capture(game_data)) }, client);
            SendGate(client);
        }

        // Chunking is transport only: a batch is made visible after its final chunk arrives.
        private void SendPresentation(ushort tag, MsgPresentation message, ClientData target = null)
        {
            const int chunkSize = 12000;
            string json = message.json;
            message.parts = Math.Max(1, (json.Length + chunkSize - 1) / chunkSize);
            for (int part = 0; part < message.parts; part++)
            {
                message.part = part;
                message.json = json.Substring(part * chunkSize, Math.Min(chunkSize, json.Length - part * chunkSize));
                using (var writer = new FastBufferWriter(128, Allocator.Temp, TcgNetwork.MsgSizeMax))
                {
                    writer.WriteValueSafe(tag);
                    writer.WriteNetworkSerializable(message);
                    if (target != null) Messaging.Send("refresh", target.client_id, writer, NetworkDelivery.ReliableFragmentedSequenced);
                    else foreach (ClientData client in connected_clients)
                        Messaging.Send("refresh", client.client_id, writer, NetworkDelivery.ReliableFragmentedSequenced);
                }
            }
        }

        private static bool IsControl(ushort type) => type == GameAction.PresentationAck || type == GameAction.PresentationSync
            || type == GameAction.ChatMessage || type == GameAction.Resign || type >= GameAction.PlayerSettings && type <= GameAction.GameSettings;

        private static bool IsSelection(ushort type) => type >= GameAction.SelectCard && type <= GameAction.CancelSelect;

        private void FinishCardRequest(ClientData client, MsgPlayRequest msg, bool accepted, long sequence)
        {
            using (var writer = new FastBufferWriter(128, Allocator.Temp, TcgNetwork.MsgSizeMax))
            {
                writer.WriteValueSafe(GameAction.ActionResult);
                writer.WriteNetworkSerializable(new MsgCardActionResult {
                    card_uid = msg.card_uid, request_id = msg.request_id, accepted = accepted, sequence = sequence });
                Messaging.Send("refresh", client.client_id, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
    }
}
