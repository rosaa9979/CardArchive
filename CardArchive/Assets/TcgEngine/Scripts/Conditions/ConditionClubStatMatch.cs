using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Compares a marker value on the caster against the same marker value on the owner's club card.
    /// Locates the owner's club card via 'club' (HasClub), then evaluates:
    ///     caster.GetMarkerValue(marker)  [oper]  clubCard.GetMarkerValue(marker)
    /// e.g. Tea Party: caster's host index == club card's active host index.
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/ClubStatMatch", order = 10)]
    public class ConditionClubStatMatch : ConditionData
    {
        public ClubData club;          //Club whose card holds the reference marker value
        [UnityEngine.Serialization.FormerlySerializedAs("trait")]
        public MarkerData marker;      //Marker compared on both the club card and the caster
        public ConditionOperatorInt oper = ConditionOperatorInt.Equal;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster, AbilityEventContext context = null)
        {
            if (club == null || marker == null)
                return false;

            Player player = data.GetPlayer(caster.player_id);

            //Locate the owner's club card that carries the reference marker value
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

            int caster_value = caster.GetMarkerValue(marker);
            int club_value = club_card.GetMarkerValue(marker);

            return CompareInt(caster_value, oper, club_value);
        }
    }
}
