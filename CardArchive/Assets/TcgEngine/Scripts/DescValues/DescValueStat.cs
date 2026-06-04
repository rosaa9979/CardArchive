using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Desc value from a card stat (attack/hp/mana/range). Uses the runtime current value when a Card
    /// is available, otherwise the card definition's base value.
    /// </summary>

    [CreateAssetMenu(fileName = "desc_value", menuName = "TcgEngine/DescValue/Stat", order = 10)]
    public class DescValueStat : DescValueData
    {
        public EffectStatType stat;

        public override string Resolve(Card card, CardData base_card)
        {
            return Map(GetStat(card, base_card));
        }

        private int GetStat(Card card, CardData base_card)
        {
            switch (stat)
            {
                case EffectStatType.Attack: return card != null ? card.GetAttack() : (base_card != null ? base_card.attack : 0);
                case EffectStatType.HP:     return card != null ? card.GetHP()     : (base_card != null ? base_card.hp : 0);
                case EffectStatType.Mana:   return card != null ? card.GetMana()   : (base_card != null ? base_card.mana : 0);
                case EffectStatType.Range:  return card != null ? card.GetRange()  : (base_card != null ? base_card.GetRange() : 0);
                default: return 0;
            }
        }
    }
}
