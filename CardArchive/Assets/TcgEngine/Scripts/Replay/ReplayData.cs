using System;
using System.Collections.Generic;

namespace TcgEngine.Replay
{
    [Serializable] public class ReplayNode
    {
        public string key;
        public string value;
        public bool isNull;
        public List<ReplayNode> children = new List<ReplayNode>();
    }
    [Serializable] public class ReplayChange
    {
        public string[] path;
        public ReplayNode value;
    }
    [Serializable] public class ReplayEntry
    {
        public string kind; // state, event, interaction
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
        public int formatVersion = 1;
        public string buildVersion;
        public string matchId;
        public string[] players;
        public ReplayNode initialState;
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
