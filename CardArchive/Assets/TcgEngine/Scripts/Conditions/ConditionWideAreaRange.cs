using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    public enum ConditionSlotType
    {
        None = 0,
        LastAttacked = 1,
        LastTargeted = 2,
        LastSummoned = 3,
        LastDestroyed = 4,
    }
    /// <summary>
    /// SlotRange check each axis variable individualy for range between the caster and target
    /// If you want to check the travel distance instead (all at once) use SlotDist
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/WideAreaRange", order = 11)]
    public class ConditionWideAreaRange : ConditionData
    {
        [Header("Reference Slot")]
        public ConditionSlotType type;

        [Header("Range")]
        public List<Direction> directions;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            Player player = data.GetPlayer(caster.player_id);
            Slot selected = Slot.None;
            List<Slot> wa_slot = new List<Slot>();

            if (type == ConditionSlotType.LastAttacked)
                selected = data.last_attacked_slot;
            if (type == ConditionSlotType.LastTargeted)
                selected = data.last_targeted_slot;
            if (type == ConditionSlotType.LastSummoned)
                selected = data.last_summoned_slot;
            if (type == ConditionSlotType.LastDestroyed)
                selected = data.last_destroyed_slot;

            Debug.Log("last_summoned_slopt : "+selected.x+" "+selected.y+" "+selected.p);
            foreach (var dir in directions)
            {
                int new_x = player.player_id == 0 ? selected.x + dir.dx : selected.x + ((-1) * dir.dx);
                int new_y = player.player_id == 0 ? selected.y + dir.dy : selected.y + ((-1) * dir.dy);

                Slot new_slot = new Slot(new SlotXY(new_x, new_y));

                if (new_slot.IsValid())
                    wa_slot.Add(new_slot);
            }
            
            return wa_slot.Contains(target);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot selected, Slot target)
        {
            Player player = data.GetPlayer(caster.player_id);
            List<Slot> wa_slot = new List<Slot>();

            foreach (var dir in directions)
            {
                int new_x = player.player_id == 0 ? selected.x + dir.dx : selected.x + ((-1) * dir.dx);
                int new_y = player.player_id == 0 ? selected.y + dir.dy : selected.y + ((-1) * dir.dy);

                Slot new_slot = new Slot(new SlotXY(new_x, new_y));

                if (new_slot.IsValid())
                    wa_slot.Add(new_slot);
            }
            
            return wa_slot.Contains(target);
        }
    }

    [System.Serializable]
    public class Direction
    {
        public int dx;
        public int dy;
    }
}