using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Condition that checks the type, team and traits of a card
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/CardType", order = 10)]
    public class ConditionCardType : ConditionData
    {
        [Header("Card is of type")]
        public List<CardType> has_type;
        public List<ClubData> has_club;
        public List<TraitData> has_trait;

        public ConditionOperatorBool oper;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            return CompareBool(IsTrait(target), oper);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            return false; //Not a card
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            Card slot_card = data.GetSlotCard(target);
            if (slot_card != null)
                return IsTargetConditionMet(data, ability, caster, slot_card);
            return false; //Not a card
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, CardData target)
        {
            /*
            bool is_type = target.type == has_type || has_type == CardType.None;
            bool is_club = target.HasClub(has_club) || has_club == null;
            bool is_trait = target.HasTrait(has_trait) || has_trait == null;
            //return (is_type && is_team && is_trait);
            return (is_type && is_club && is_trait);
            */

            bool is_type = has_type.Contains(target.type) || has_type.Count == 0;
            bool is_club = has_club.Any(club => target.HasClub(club)) || has_club.Count == 0;
            bool is_trait = has_trait.Any(trait => target.HasTrait(trait)) || has_trait.Count == 0;
            //return (is_type && is_team && is_trait);
            return (is_type && is_club && is_trait);
        }

        private bool IsTrait(Card card)
        {
            bool is_type = has_type.Contains(card.CardData.type) || has_type.Count == 0;
            bool is_club = has_club.Any(club => card.HasClub(club)) || has_club.Count == 0;
            bool is_trait = has_trait.Any(trait => card.HasTrait(trait)) || has_trait.Count == 0;
            //return (is_type && is_team && is_trait);
            return (is_type && is_club && is_trait);
        }
    }
}