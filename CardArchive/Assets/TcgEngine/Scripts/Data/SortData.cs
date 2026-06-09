using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Base class for target sorters.
    /// Reorders the list of targets (already picked by conditions and filters) before effects are applied.
    /// The default implementation keeps the original order — override to sort by a criterion.
    /// Ascending / descending is controlled by 'descending' (shared by every sorter).
    /// </summary>

    public class SortData : ScriptableObject
    {
        [Tooltip("false = 오름차순(작은 값이 앞), true = 내림차순(큰 값이 앞)")]
        public bool descending = false;

        public virtual List<Card> SortTargets(Game data, AbilityData ability, Card caster, List<Card> source)
        {
            return source; //Override this, sort targeting card
        }

        public virtual List<Player> SortTargets(Game data, AbilityData ability, Card caster, List<Player> source)
        {
            return source; //Override this, sort targeting player
        }

        public virtual List<Slot> SortTargets(Game data, AbilityData ability, Card caster, List<Slot> source)
        {
            return source; //Override this, sort targeting slot
        }

        public virtual List<CardData> SortTargets(Game data, AbilityData ability, Card caster, List<CardData> source)
        {
            return source; //Override this, sort targeting card data
        }

        //Apply the ascending/descending direction to a CompareTo result (negate when descending).
        protected int Direction(int compare)
        {
            return descending ? -compare : compare;
        }
    }
}
