using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// 카드가 배치될 수 있는지 확인
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/CanPlace", order = 11)]
    public class ConditionCanPlace : ConditionData
    {
        [Header("Custom Card Data")]
        public BoardDestinationPurpose purpose;
        public ConditionLastType last_type;
        public CardData place_card;
        public ConditionPlayerType card_owner;

        [Header("Operation")]
        public ConditionOperatorBool oper;

        private bool CanUse(Game data, Card card, Slot slot)
        {
            return purpose == BoardDestinationPurpose.Movement
                ? data.CanMoveCard(card, slot) : data.CanPlaceCard(card, slot);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target, AbilityEventContext context = null)
        {
            return target != null && IsTargetConditionMet(data, ability, caster, target.slot, context);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target, AbilityEventContext context = null)
        {
            var execution = context as AbilityExecutionContext;
            Card source = caster;
            if (last_type == ConditionLastType.LastSelected)
                source = data.GetCard(execution?.last_selected ?? data.last_selected);
            else if (last_type == ConditionLastType.LastTargeted)
                source = execution != null ? data.GetCard(execution.reference_target_uid) : data.GetSlotCard(data.last_targeted_slot);
            else if (last_type == ConditionLastType.None && place_card != null)
            {
                Player owner = card_owner == ConditionPlayerType.Self ? data.GetPlayer(caster.player_id)
                    : card_owner == ConditionPlayerType.Opponent ? data.GetOpponentPlayer(caster.player_id) : null;
                return CompareBool(purpose == BoardDestinationPurpose.Placement && owner != null
                    && data.CanPlaceCard(place_card, owner.player_id, target), oper);
            }
            return CompareBool(CanUse(data, source, target), oper);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target, AbilityEventContext context = null)
        {
            return false;
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card selected, Slot target, AbilityEventContext context = null)
        {
            return CompareBool(CanUse(data, selected, target), oper);
        }
    }
}
