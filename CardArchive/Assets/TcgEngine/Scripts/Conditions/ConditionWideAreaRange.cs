using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// SlotRange check each axis variable individualy for range between the caster and target
    /// If you want to check the travel distance instead (all at once) use SlotDist
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/WideAreaRange", order = 11)]
    public class ConditionWideAreaRange : ConditionData
    {
        [Header("Range")]
        public List<Direction> directions;

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