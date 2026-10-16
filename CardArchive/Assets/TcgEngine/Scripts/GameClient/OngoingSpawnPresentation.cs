using System;
using System.Collections.Generic;

namespace TcgEngine.Client
{
    // View-only spawn gates. Keys include re-entry order; stale callbacks cannot open a new spawn.
    public sealed class OngoingSpawnPresentation
    {
        private readonly Dictionary<(string, int), object> pending = new Dictionary<(string, int), object>();
        public bool CanShow(Card card) => card != null && !pending.ContainsKey((card.uid, card.play_order));

        public Action Begin(Card card)
        {
            var key = (card.uid, card.play_order);
            var token = new object();
            pending[key] = token;
            return () => {
                if (pending.TryGetValue(key, out var current) && current == token) pending.Remove(key);
            };
        }

        public void Clear() { pending.Clear(); }
    }
}
