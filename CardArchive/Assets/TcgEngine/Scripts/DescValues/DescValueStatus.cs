using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Desc value from a card status value. Status only exists on the runtime Card, so in static contexts
    /// (no Card) it resolves to 0 (then mapped). Pair with a mapping to print a name, e.g. host index -> student.
    /// </summary>

    [CreateAssetMenu(fileName = "desc_value", menuName = "TcgEngine/DescValue/Status", order = 10)]
    public class DescValueStatus : DescValueData
    {
        public StatusData status;

        public override string Resolve(Card card, CardData base_card)
        {
            if (status == null)
                return Map(0);

            int v = card != null ? card.GetStatusValue(status.effect) : 0;
            return Map(v);
        }
    }
}
