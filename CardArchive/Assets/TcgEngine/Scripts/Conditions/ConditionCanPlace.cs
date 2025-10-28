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
        public ConditionLastType last_type;
        public CardData place_card;
        public ConditionPlayerType card_owner;

        [Header("Operation")]
        public ConditionOperatorBool oper;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            return CompareBool(data.CanPlaceCard(caster, target.slot), oper);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            if (last_type == ConditionLastType.LastSelected)
            {
                Card last_selected = data.GetCard(data.last_selected);
                if (last_selected != null)
                    return CompareBool(data.CanPlaceCard(last_selected, target), oper);
            }

            if (last_type == ConditionLastType.LastTargeted)
            {
                Card last_targeted = data.GetSlotCard(data.last_targeted_slot);
                if (last_targeted != null)
                    return CompareBool(data.CanPlaceCard(last_targeted, target), oper);
            }

            if (last_type == ConditionLastType.None && place_card != null)
            {
                Player player = null;
                if (card_owner == ConditionPlayerType.Self)
                    player = data.GetPlayer(caster.player_id);
                if (card_owner == ConditionPlayerType.Opponent)
                    player = data.GetOpponentPlayer(caster.player_id);

                Card card = Card.Create(place_card, VariantData.GetDefault(), player);
                return CompareBool(data.CanPlaceCard(card, target), oper);
            }

            return CompareBool(data.CanPlaceCard(caster, target), oper);
        }
        
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card selected, Slot target)
        {
            return CompareBool(data.CanPlaceCard(selected, target), oper);
        }
    }
}