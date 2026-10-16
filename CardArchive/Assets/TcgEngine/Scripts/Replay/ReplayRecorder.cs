using System;
using System.Linq;
using UnityEngine;

namespace TcgEngine.Replay
{
    public sealed class ReplayRecorder
    {
        public ReplayRecord Record { get; private set; }
        ReplayState previous;
        public bool Finished { get; private set; }
        public bool Failed { get; private set; }
        public void Begin(Game game) { Begin(game, Application.version); }
        public void Begin(Game game, string buildVersion)
        {
            Guard(() => {
                previous = ReplayStateCodec.Capture(game);
                Record = new ReplayRecord { matchId = game.game_uid, buildVersion = buildVersion,
                    players = game.players.Select(p => p.username).ToArray(), initialState = previous };
            });
        }
        // Records field changes, not repeated full snapshots. Called at semantic boundaries,
        // including every resolved effect and server event, never on AI prediction instances.
        public void Capture(Game game)
        {
            if (Record == null || Finished || Failed) return;
            Guard(() => {
                var next = ReplayStateCodec.Capture(game);
                var changes = ReplayStateCodec.Diff(previous, next);
                if (changes.Count > 0) Record.events.Add(new ReplayEntry { kind = "state", changes = changes });
                previous = next;
            });
        }
        public void Event(Game game, byte[] packet)
        {
            Capture(game);
            if (Record != null && !Finished && !Failed)
                Record.events.Add(new ReplayEntry { kind = "event", packet = Convert.ToBase64String(packet) });
        }
        public void Interaction(Game game, ReplayInteraction interaction)
        {
            PresentationBoundary(game);
            if (Record != null && !Finished && !Failed)
                Record.events.Add(new ReplayEntry { kind = "interaction", interaction = interaction });
        }

        // Raw event snapshots can be taken midway through an effect (damage before
        // lifesteal, death attribution, etc.). Preserve the live publication boundary
        // so playback never exposes those intermediate values before the target FX.
        public void PresentationBoundary(Game game)
        {
            Capture(game);
            if (Record == null || Finished || Failed || Record.events.Count == 0) return;
            string last = Record.events[Record.events.Count - 1].kind;
            if (last != "presentation" && last != "interaction")
                Record.events.Add(new ReplayEntry { kind = "presentation" });
        }
        public ReplayRecord Finish(Game game)
        {
            PresentationBoundary(game);
            Finished = true;
            if (Record == null || Failed) return null;
            Record.finalHash = ReplayStateCodec.Hash(previous);
            return Record;
        }
        public void Random(string value)
        {
            if (Record != null && !Finished && !Failed)
                Record.events.Add(new ReplayEntry { kind = "random", randomResult = value });
        }
        void Guard(Action action)
        {
            try { action(); }
            catch (Exception e) { Failed = true; Debug.LogError("Replay recording failed; match continues: " + e); }
        }
    }
}
