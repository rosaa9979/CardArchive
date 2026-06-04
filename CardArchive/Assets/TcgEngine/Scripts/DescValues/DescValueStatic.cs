using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Desc value that is a fixed piece of text. The int->label mapping is ignored.
    /// </summary>

    [CreateAssetMenu(fileName = "desc_value", menuName = "TcgEngine/DescValue/Static", order = 10)]
    public class DescValueStatic : DescValueData
    {
        public string text;

        public override string Resolve(Card card, CardData base_card)
        {
            return text;
        }
    }
}
