using System;
using System.Collections.Generic;

namespace TcgEngine.Replay
{
    public enum ReplayEntityKind { Game, Player, Card }

    // Normalized tables. Entity bodies use the bounded, versioned field schema in
    // ReplayStateCodec; relationships are IDs, never nested entities.
    [Serializable] public class ReplayState
    {
        public int formatVersion = 2;
        public List<ReplayEntity> entities = new List<ReplayEntity>();
    }
    [Serializable] public class ReplayEntity
    {
        public ReplayEntityKind kind;
        public string id;
        public string data;
    }
    [Serializable] public class ReplayChange
    {
        public ReplayEntity entity;
        public bool removed;
    }
    [Serializable] public class ReplayEntry
    {
        public string kind; // state, event, interaction, random, presentation (atomic display boundary)
        public List<ReplayChange> changes;
        public string packet;
        public ReplayInteraction interaction;
        public string randomResult;
    }
    [Serializable] public class ReplayInteraction
    {
        public string kind;
        public int player;
        public string card;
        public string target;
        public Slot slot;
        public int choice = -1;
        public string[] cards;
    }
    [Serializable] public class ReplayRecord
    {
        public int formatVersion = 2;
        public string buildVersion;
        public string matchId;
        public string[] players;
        public ReplayState initialState;
        public List<ReplayEntry> events = new List<ReplayEntry>();
        public string finalHash;
    }
    [Serializable] public class ReplayUpload
    {
        public string matchId;
        public string[] players;
        public string payload;
    }
    [Serializable] public class ReplayList { public string[] ids; }
}
