using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Internal value used by rule logic (counters, stored values, branch marks, bonus parameters).
    /// Never displayed as a tag: players only see it through card text or desc values.
    /// Tribes are TraitData, rule keywords and conditions are StatusData.
    /// </summary>

    [CreateAssetMenu(fileName = "MarkerData", menuName = "TcgEngine/MarkerData", order = 1)]
    public class MarkerData : ScriptableObject
    {
        public string id;
        public string title; //Editor and debug label only

        [TextArea(2, 4)]
        public string desc;  //Editor note: who writes and who reads this value

        public static List<MarkerData> marker_list = new List<MarkerData>();

        public static void Load(string folder = "")
        {
            if (marker_list.Count == 0)
                marker_list.AddRange(Resources.LoadAll<MarkerData>(folder));
        }

        public static MarkerData Get(string id)
        {
            foreach (MarkerData marker in GetAll())
            {
                if (marker.id == id)
                    return marker;
            }
            return null;
        }

        public static List<MarkerData> GetAll()
        {
            return marker_list;
        }
    }

    [System.Serializable]
    public struct MarkerValue
    {
        [UnityEngine.Serialization.FormerlySerializedAs("trait")]
        public MarkerData marker;
        public int value;
    }
}
