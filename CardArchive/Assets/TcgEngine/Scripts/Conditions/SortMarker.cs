using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Sorts targets by a marker value (Card / Player GetMarkerValue).
    /// Example: marker = tea_party_host, descending = false  =>  티파티 호스트 값 오름차순 정렬.
    /// Targets without the marker count as 0.
    /// </summary>

    [CreateAssetMenu(fileName = "sort", menuName = "TcgEngine/Sort/Marker", order = 10)]
    public class SortMarker : SortData
    {
        [UnityEngine.Serialization.FormerlySerializedAs("trait")]
        public MarkerData marker;

        public override List<Card> SortTargets(Game data, AbilityData ability, Card caster, List<Card> source)
        {
            if (marker == null || source == null)
                return source;
            source.Sort((a, b) => Direction(a.GetMarkerValue(marker).CompareTo(b.GetMarkerValue(marker))));
            return source;
        }

        public override List<Player> SortTargets(Game data, AbilityData ability, Card caster, List<Player> source)
        {
            if (marker == null || source == null)
                return source;
            source.Sort((a, b) => Direction(a.GetMarkerValue(marker).CompareTo(b.GetMarkerValue(marker))));
            return source;
        }
    }
}
