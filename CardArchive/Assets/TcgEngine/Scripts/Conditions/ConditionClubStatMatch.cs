using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Compares a trait (stat) value on the caster against the same trait value on the owner's club card.
    /// Locates the owner's club card via 'club' (HasClub), then evaluates:
    ///     caster.GetTraitValue(trait)  [oper]  clubCard.GetTraitValue(trait)
    /// e.g. Tea Party: caster's host index == club card's active host index.
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/ClubStatMatch", order = 10)]
    public class ConditionClubStatMatch : ConditionData
    {
        public ClubData club;          //Club whose card holds the reference trait value
        public TraitData trait;        //Trait compared on both the club card and the caster
        public ConditionOperatorInt oper = ConditionOperatorInt.Equal;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            if (club == null || trait == null)
                return false;

            Player player = data.GetPlayer(caster.player_id);

            //Locate the owner's club card that carries the reference trait value
            Card club_card = null;
            foreach (Card card in player.cards_club)
            {
                if (card.HasClub(club))
                {
                    club_card = card;
                    break;
                }
            }

            if (club_card == null)
                return false;

            int caster_value = caster.GetTraitValue(trait);
            int club_value = club_card.GetTraitValue(trait);

            return CompareInt(caster_value, oper, club_value);
        }
    }
}
