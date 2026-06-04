using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Base class for a single value substituted into a CardData.desc placeholder ({0}, {1}, ...).
    /// Subclasses resolve the value from a different source (stat / trait / status / static text).
    /// An optional int->label mapping converts a numeric value into a name (e.g. host index -> student name).
    /// Resolve is called with the runtime Card when available (board/preview), or null in static contexts
    /// (deckbuilder), in which case base_card (the card definition) is used for the fallback.
    /// </summary>

    public abstract class DescValueData : ScriptableObject
    {
        public StringMapping[] mapping;   //Optional: int value -> label

        public abstract string Resolve(Card card, CardData base_card);

        //Shared helper: map a numeric value to a label, or return the number as text if unmapped
        protected string Map(int value)
        {
            if (mapping != null)
            {
                foreach (StringMapping m in mapping)
                {
                    if (m.value == value)
                        return m.label;
                }
            }
            return value.ToString();
        }
    }

    [System.Serializable]
    public struct StringMapping
    {
        public int value;
        public string label;
    }
}
