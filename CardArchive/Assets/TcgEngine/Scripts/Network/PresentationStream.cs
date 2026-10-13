using System;
using System.Collections.Generic;
using TcgEngine.Replay;
using Unity.Netcode;

namespace TcgEngine
{
    // A frame is a rule boundary, never a server-side animation instruction.
    [Serializable] public sealed class PresentationFrame
    {
        public long sequence;
        // Nonzero only for a single, otherwise uninterrupted normal draw outcome.
        public long drawRequestId;
        public int drawPlayerId;
        public List<ReplayChange> changes;
        public List<string> events = new List<string>();
    }

    [Serializable] public sealed class PresentationBatch
    {
        public List<PresentationFrame> frames = new List<PresentationFrame>();
    }

    public sealed class PresentationRecorder
    {
        private ReplayState previous;
        private readonly List<string> events = new List<string>();
        private PresentationBatch pending = new PresentationBatch();
        private long drawRequestId;
        private int drawPlayerId;
        private bool onlyDrawEvents = true;
        public long Sequence { get; private set; }
        public void Begin(Game game) { previous = ReplayStateCodec.Capture(game); }
        public void MarkDraw(long requestId, int playerId)
        {
            drawRequestId = requestId;
            drawPlayerId = playerId;
        }
        public void Event(ushort tag, byte[] packet)
        {
            events.Add(Convert.ToBase64String(packet));
            onlyDrawEvents &= tag == GameAction.CardDrawn;
        }
        public void Capture(Game game)
        {
            var next = ReplayStateCodec.Capture(game);
            var changes = ReplayStateCodec.Diff(previous, next);
            if (changes.Count != 0 || events.Count != 0)
                pending.frames.Add(new PresentationFrame {
                    sequence = ++Sequence, changes = changes, events = new List<string>(events),
                    drawRequestId = onlyDrawEvents && events.Count == 1 ? drawRequestId : 0,
                    drawPlayerId = drawPlayerId });
            events.Clear();
            drawRequestId = 0;
            onlyDrawEvents = true;
            previous = next;
        }
        public PresentationBatch Take()
        {
            var result = pending;
            pending = new PresentationBatch();
            return result;
        }
    }

    // Presentation groups never alter stored outcomes or authoritative state boundaries.
    public static class PresentationPlayback
    {
        public static List<PresentationFrame> DequeueGroup(Queue<PresentationFrame> queue)
        {
            var first = queue.Dequeue();
            var group = new List<PresentationFrame> { first };
            if (first.drawRequestId <= 0) return group;
            long lastSequence = first.sequence;
            while (queue.Count > 0)
            {
                var next = queue.Peek();
                if (next.drawRequestId != first.drawRequestId || next.drawPlayerId != first.drawPlayerId
                    || next.sequence != lastSequence + 1) break;
                group.Add(queue.Dequeue());
                lastSequence = next.sequence;
            }
            return group;
        }
    }

    // Only authenticated participating humans count. Disconnecting never completes a barrier.
    public sealed class PresentationBarrier
    {
        private readonly HashSet<int> waiting = new HashSet<int>();
        public long Id { get; private set; }
        public long Sequence { get; private set; }
        public bool Active { get; private set; }
        public bool Complete => Active && waiting.Count == 0;
        public void Begin(long sequence, IEnumerable<int> participants)
        {
            if (Active) throw new InvalidOperationException("Presentation barrier already active");
            Id++;
            Sequence = sequence;
            waiting.Clear();
            foreach (int player in participants) waiting.Add(player);
            Active = true;
        }
        public void Acknowledge(int player, long id, long sequence)
        {
            if (Active && Id == id && Sequence == sequence) waiting.Remove(player);
        }
        public void Release() { Active = false; waiting.Clear(); }
    }

    public sealed class MsgSelectSlotRequest : INetworkSerializable
    {
        public Slot slot;
        public string target_uid = "";
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeNetworkSerializable(ref slot);
            serializer.SerializeValue(ref target_uid);
        }
    }

    public sealed class MsgPlayRequest : MsgPlayCard, INetworkSerializable
    {
        public string target_uid = "";
        public new void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeValue(ref target_uid);
        }
    }

    public sealed class MsgPresentation : INetworkSerializable
    {
        public long sequence;
        public long barrier;
        public bool closed;
        public float timer;
        public bool reset;
        public string json = "";
        public int part;
        public int parts = 1;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref sequence);
            serializer.SerializeValue(ref barrier);
            serializer.SerializeValue(ref closed);
            serializer.SerializeValue(ref timer);
            serializer.SerializeValue(ref reset);
            serializer.SerializeValue(ref json);
            serializer.SerializeValue(ref part);
            serializer.SerializeValue(ref parts);
        }
    }
}
