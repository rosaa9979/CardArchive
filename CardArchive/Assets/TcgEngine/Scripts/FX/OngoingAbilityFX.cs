using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.FX
{
    // View-only reconciliation. Call with the published snapshot; never executes ability effects/conditions.
    public sealed class OngoingAbilityFX
    {
        private sealed class Entry { public GameObject prefab; public GameObject instance; }
        private readonly Transform anchor;
        private readonly Dictionary<AbilityData, Entry> entries = new Dictionary<AbilityData, Entry>();
        private readonly HashSet<AbilityData> wanted = new HashSet<AbilityData>();
        private readonly List<AbilityData> removed = new List<AbilityData>();

        public OngoingAbilityFX(Transform anchor) { this.anchor = anchor; }

        public void Sync(Game displayed, Card caster, bool viewRemoved)
        {
            wanted.Clear();
            if (anchor != null && displayed != null && caster != null && !viewRemoved
                && displayed.IsOnBoard(caster) && caster.CanDoAbilities())
            {
                foreach (AbilityData ability in caster.GetAbilities())
                {
                    if (ability == null || ability.trigger != AbilityTrigger.Ongoing || ability.ongoing_fx == null || !wanted.Add(ability)) continue;
                    if (!entries.TryGetValue(ability, out var entry) || entry.instance == null || entry.prefab != ability.ongoing_fx)
                    {
                        if (entry != null) Remove(entry.instance);
                        entry = new Entry { prefab = ability.ongoing_fx,
                            instance = FXTool.DoPersistentFX(ability.ongoing_fx, anchor, caster.player_id, preserveWorldSize: true) };
                        entries[ability] = entry;
                    }
                    FXTool.SetOwner(entry.instance, caster.player_id);
                }
            }
            removed.Clear();
            foreach (var pair in entries)
                if (!wanted.Contains(pair.Key))
                {
                    Remove(pair.Value.instance);
                    removed.Add(pair.Key);
                }
            foreach (var ability in removed) entries.Remove(ability);
        }

        public void Clear()
        {
            foreach (var entry in entries.Values) Remove(entry.instance);
            entries.Clear();
            wanted.Clear();
            removed.Clear();
        }

        private static void Remove(GameObject fx)
        {
            if (fx == null) return;
            fx.SetActive(false);
            Object.Destroy(fx);
        }
    }
}
