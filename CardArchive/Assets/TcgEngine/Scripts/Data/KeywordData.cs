using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Defines a keyword explained next to a card preview (등교, 난사...). Display only, no gameplay effect.
    /// Abilities list the keywords their text shows, statuses point to the keyword they apply to the card.
    /// </summary>

    [CreateAssetMenu(fileName = "keyword", menuName = "TcgEngine/KeywordData", order = 7)]
    public class KeywordData : ScriptableObject
    {
        public string id;
        public string title;

        [TextArea(3, 5)]
        public string desc;

        public string GetTitle()
        {
            return title;
        }

        public string GetDesc()
        {
            return desc;
        }
    }
}
