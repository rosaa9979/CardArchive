using System;
using System.Collections.Generic;

namespace TcgEngine
{
    // One invocation owns this state across queue suspension and repeat iterations.
    // The originating event remains immutable and is shared only through its value snapshot.
    [Serializable]
    public sealed class AbilityExecutionContext : AbilityEventContext
    {
        public readonly AbilityEventContext source_event;
        public readonly string trigger_uid;
        public readonly int trigger_player;
        public readonly string last_played;
        public readonly string last_selected;
        public readonly Slot last_attacked_slot;
        public readonly bool last_player_attacked;
        public readonly Slot last_summoned_slot;
        public readonly Slot last_destroyed_slot;
        public readonly Slot reference_target_slot;
        public readonly string reference_target_uid;
        public readonly List<Slot> parent_slots;
        public readonly List<Slot> resolved_slots = new List<Slot>();
        public string resolved_target_uid;
        public Slot resolved_target_slot = Slot.None;
        public int iteration;
        [NonSerialized] public bool is_query;

        // Runtime predicates refer to loaded definitions. Network/replay snapshots carry only values.
        [NonSerialized] private Dictionary<ConditionData, Dictionary<string, bool>> results
            = new Dictionary<ConditionData, Dictionary<string, bool>>();

        public AbilityExecutionContext(Game data, Card trigger, Player player, AbilityEventContext origin)
            : this(data, trigger, player, origin, null) { }

        private AbilityExecutionContext(Game data, Card trigger, Player player, AbilityEventContext origin, Card pendingPlay)
        {
            AbilityExecutionContext parent = origin as AbilityExecutionContext;
            source_event = Event(origin)?.Clone();
            trigger_uid = trigger != null ? trigger.uid : null;
            trigger_player = player != null ? player.player_id : -1;
            last_played = pendingPlay != null ? pendingPlay.uid : data.last_played;
            last_selected = data.last_selected;
            last_attacked_slot = data.last_attacked_slot;
            last_player_attacked = data.last_player_attacked;
            last_summoned_slot = pendingPlay != null && pendingPlay.CardData.IsBoardCard() ? pendingPlay.slot : data.last_summoned_slot;
            last_destroyed_slot = data.last_destroyed_slot;
            reference_target_slot = parent != null ? parent.resolved_target_slot : data.last_targeted_slot;
            reference_target_uid = parent != null ? parent.resolved_target_uid : data.last_target;
            parent_slots = parent != null ? new List<Slot>(parent.resolved_slots) : new List<Slot>();
        }

        public static AbilityExecutionContext PreparePlay(Game data, Card card)
        {
            return new AbilityExecutionContext(data, card, null, null, card);
        }

        private AbilityExecutionContext(AbilityExecutionContext other)
        {
            source_event = other.source_event?.Clone();
            trigger_uid = other.trigger_uid;
            trigger_player = other.trigger_player;
            last_played = other.last_played;
            last_selected = other.last_selected;
            last_attacked_slot = other.last_attacked_slot;
            last_player_attacked = other.last_player_attacked;
            last_summoned_slot = other.last_summoned_slot;
            last_destroyed_slot = other.last_destroyed_slot;
            reference_target_slot = other.reference_target_slot;
            reference_target_uid = other.reference_target_uid;
            parent_slots = new List<Slot>(other.parent_slots);
            resolved_slots.AddRange(other.resolved_slots);
            resolved_target_uid = other.resolved_target_uid;
            resolved_target_slot = other.resolved_target_slot;
            iteration = other.iteration;
            is_query = other.is_query;
            if (other.results != null)
                foreach (var item in other.results)
                    results.Add(item.Key, new Dictionary<string, bool>(item.Value));
        }

        public static AbilityEventContext Event(AbilityEventContext context)
        {
            return context is AbilityExecutionContext execution ? execution.source_event : context;
        }

        public bool Evaluate(ConditionData condition, string key, Func<bool> predicate)
        {
            if (is_query)
            {
                if (condition.evaluation == ConditionEvaluationPolicy.EvaluateOnce && results != null
                    && results.TryGetValue(condition, out var existing) && existing.TryGetValue(key, out bool cached))
                    return cached;
                return predicate();
            }
            if (condition.evaluation == ConditionEvaluationPolicy.Revalidate)
                return predicate();
            if (results == null) results = new Dictionary<ConditionData, Dictionary<string, bool>>();
            if (!results.TryGetValue(condition, out var cache))
                results.Add(condition, cache = new Dictionary<string, bool>());
            if (!cache.TryGetValue(key, out bool result))
                cache.Add(key, result = predicate());
            return result;
        }

        public static AbilityEventContext Query(AbilityEventContext context)
        {
            if (!(context is AbilityExecutionContext execution) || execution.is_query) return context;
            var query = (AbilityExecutionContext)execution.Clone();
            query.is_query = true;
            return query;
        }

        public void BeginIteration(int index)
        {
            iteration = index;
            resolved_slots.Clear();
            resolved_target_uid = null;
            resolved_target_slot = Slot.None;
        }

        public void RecordTarget(Slot slot, string uid = null)
        {
            resolved_target_slot = slot;
            resolved_target_uid = uid;
            if (slot.IsValid() && !resolved_slots.Contains(slot)) resolved_slots.Add(slot);
        }

        public override AbilityEventContext Clone() { return new AbilityExecutionContext(this); }
    }
}
