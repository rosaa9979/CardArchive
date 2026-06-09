using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Sorts targets by a custom trait value (Card / Player GetTraitValue).
    /// Example: trait = tea_party_host, descending = false  =>  티파티 호스트 값 오름차순 정렬.
    /// Targets without the trait count as 0.
    /// </summary>

    [CreateAssetMenu(fileName = "sort", menuName = "TcgEngine/Sort/Trait", order = 10)]
    public class SortTrait : SortData
    {
        public TraitData trait;

        public override List<Card> SortTargets(Game data, AbilityData ability, Card caster, List<Card> source)
        {
            if (trait == null || source == null)
                return source;
            source.Sort((a, b) => Direction(a.GetTraitValue(trait).CompareTo(b.GetTraitValue(trait))));
            return source;
        }

        public override List<Player> SortTargets(Game data, AbilityData ability, Card caster, List<Player> source)
        {
            if (trait == null || source == null)
                return source;
            source.Sort((a, b) => Direction(a.GetTraitValue(trait).CompareTo(b.GetTraitValue(trait))));
            return source;
        }
    }
}
