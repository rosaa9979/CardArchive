using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// ability의 widerangearea condition에서만 사용하며, 선택한 슬롯을 기점으로 효과 범위를 정의한다
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/WideAreaRange", order = 11)]
    public class ConditionWideAreaRange : ConditionData
    {
        [Header("Range")]
        public List<Direction> directions;
        public Sprite thumnail;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            Player player = data.GetPlayer(caster.player_id);
            Slot selected = Slot.None;
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