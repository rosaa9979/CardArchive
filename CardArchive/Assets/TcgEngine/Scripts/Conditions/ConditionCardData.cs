using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Condition that checks the card data matches
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/CardData", order = 10)]
    public class ConditionCardData : ConditionData
    {
        [Header("Card is")]
        public List<CardData> card_types;

        public ConditionOperatorBool oper;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            /*
            foreach(CardData card_type in card_types)
                if (CompareBool(target.card_id == card_type.id, oper))
                    return true;
            return false;
            */

            bool exists = card_types.Any(card => card.id == target.card_id);

            Debug.Log(exists);

            // oper 값에 따라 조건을 검사
            if (oper == ConditionOperatorBool.IsTrue && exists)
            {
                return true;  // oper이 True이고, card_id가 리스트에 존재하는 경우
            }
            else if (oper == ConditionOperatorBool.IsFalse && !exists)
            {
                return true;  // oper이 False이고, card_id가 리스트에 없는 경우
            }

            return false;  // 조건을 만족하지 않는 경우
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, CardData target)
        {
            /*
            foreach(CardData card_type in card_types)
                if (CompareBool(target.card_id == card_type.id, oper))
                    return true;
            return false;
            */

            bool exists = card_types.Any(card => card.id == target.id);

            // oper 값에 따라 조건을 검사
            if (oper == ConditionOperatorBool.IsTrue && exists)
            {
                return true;  // oper이 True이고, card_id가 리스트에 존재하는 경우
            }
            else if (oper == ConditionOperatorBool.IsFalse && !exists)
            {
                return true;  // oper이 False이고, card_id가 리스트에 없는 경우
            }

            return false;  // 조건을 만족하지 않는 경우
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            return false; //Not a card
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            return false; //Not a card
        }
    }
}