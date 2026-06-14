using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    /// <summary>
    /// Visual representation of a card in your collection in the Deckbuilder
    /// </summary>

    public class CollectionCard : MonoBehaviour
    {
        public CardUI card_ui;
        public Image quantity_bar;
        public Text quantity;

        [Header("Mat")]
        public Material color_mat;
        public Material grayscale_mat;

        [Header("Dimmed")]
        [SerializeField] private GameObject dimmed; //Shown when the card can't be added to the deck (club full / already at max in deck)

        public UnityAction<CardUI> onClick;
        public UnityAction<CardUI> onClickRight;

        private void Start()
        {
            card_ui.onClick += onClick;
            card_ui.onClickRight += onClickRight;
        }

        public void SetCard(CardData card, VariantData variant, int quantity)
        {
            card_ui.SetCard(card, variant);
            SetQuantity(quantity);
        }

        public void SetQuantity(int quantity)
        {
            if (this.quantity_bar != null)
                this.quantity_bar.enabled = quantity > 0;
            if (this.quantity != null)
                this.quantity.text = quantity.ToString();
            if (this.quantity != null)
                this.quantity.enabled = quantity > 0;
        }

        public void SetGrayscale(bool grayscale)
        {
            if (grayscale)
            {
                quantity_bar.material = grayscale_mat;
                card_ui.SetMaterial(grayscale_mat);
            }
            else
            {
                quantity_bar.material = color_mat;
                card_ui.SetMaterial(color_mat);
            }
        }

        public void SetDimmed(bool dim)
        {
            if (dimmed != null)
                dimmed.SetActive(dim);
        }

        public CardData GetCard()
        {
            return card_ui.GetCard();
        }

        public VariantData GetVariant()
        {
            return card_ui.GetVariant();
        }
    }
}