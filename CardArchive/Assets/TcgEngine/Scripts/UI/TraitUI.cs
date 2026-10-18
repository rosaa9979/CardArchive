using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    /// <summary>
    /// Shows a tribe trait tag, add it to the array of a CardUI
    /// </summary>

    public class TraitUI : MonoBehaviour
    {
        public TraitData trait;
        public Image bg;
        public Text text;

        void Start()
        {

        }

        public void SetCard(Card card)
        {
            bool has_trait = card.HasTrait(trait);
            text.text = trait != null ? trait.GetTitle() : "";
            bg.enabled = has_trait;
            text.enabled = has_trait;
        }

        public void SetCard(CardData card)
        {
            bool has_trait = card.HasTrait(trait);
            text.text = trait != null ? trait.GetTitle() : "";
            bg.enabled = has_trait;
            text.enabled = has_trait;
        }
    }
}
