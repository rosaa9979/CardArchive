using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Desc value from a card marker value. Uses the runtime marker value when a Card is
    /// available, otherwise the card definition's base marker value.
    /// </summary>

    [CreateAssetMenu(fileName = "desc_value", menuName = "TcgEngine/DescValue/Stat", order = 10)]
    public class DescValueStat : DescValueData
    {
        [UnityEngine.Serialization.FormerlySerializedAs("trait")]
        public MarkerData marker;

        public override string Resolve(Card card, CardData base_card)
        {
            if (marker == null)
                return Map(0);

            int v = card != null ? card.GetMarkerValue(marker) : (base_card != null ? base_card.GetMarkerValue(marker) : 0);
            return Map(v);
        }
    }
}
