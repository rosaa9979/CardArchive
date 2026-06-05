using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Desc value from a card trait (custom stat) value. Uses the runtime trait value when a Card is
    /// available, otherwise the card definition's base stat value.
    /// </summary>

    [CreateAssetMenu(fileName = "desc_value", menuName = "TcgEngine/DescValue/Stat", order = 10)]
    public class DescValueStat : DescValueData
    {
        public TraitData trait;

        public override string Resolve(Card card, CardData base_card)
        {
            if (trait == null)
                return Map(0);

            int v = card != null ? card.GetTraitValue(trait) : (base_card != null ? base_card.GetStat(trait) : 0);
            return Map(v);
        }
    }
}
