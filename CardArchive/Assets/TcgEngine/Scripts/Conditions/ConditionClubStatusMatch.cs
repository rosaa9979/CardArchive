using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Compares a status value on the caster against the same status value on the owner's club card.
    /// Locates the owner's club card via 'club' (HasClub), then evaluates:
    ///     caster.GetStatusValue(status)  [oper]  clubCard.GetStatusValue(status)
    /// e.g. Tea Party: caster's faction index == club card's active faction index.
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/ClubStatusMatch", order = 10)]
    public class ConditionClubStatusMatch : ConditionData
    {
        public ClubData club;          //Club whose card holds the reference status value
        public StatusData status;      //Status type compared on both the club card and the caster
        public ConditionOperatorInt oper = ConditionOperatorInt.Equal;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            if (club == null || status == null)
                return false;

            Player player = data.GetPlayer(caster.player_id);

            //Locate the owner's club card that carries the reference status value
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

            StatusType type = status.effect;
            int caster_value = caster.GetStatusValue(type);
            int club_value = club_card.GetStatusValue(type);

            return CompareInt(caster_value, oper, club_value);
        }
    }
}
