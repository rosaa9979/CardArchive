using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine;

namespace TcgEngine.UI
{
    /// <summary>
    /// The UI for card selector, appears when an ability with CardSelector target is triggered
    /// </summary>

    public class MulliganSelector : UIPanel
    {
        public RectTransform content;
        public GameObject card_template;
        public Text title;
        public Text subtitle;
        public GameObject select_button;
        public Image select_image;
        public Text select_button_text;
        public Text mulligan_timer;

        public Sprite before_select_icon;
        public Sprite after_select_icon;

        private AbilityData iability;
        private List<Card> mulligan_cards;
        private List<Card> discarded_cards;
        private List<CardSelectorCard> card_img_list = new List<CardSelectorCard>();

        private Vector2 mouse_start;
        private int mouse_index_start;
        //private bool drag = false;
        //private float mouse_scroll = 0f;
        private float timer = 0f;

        private int current_index = 0;

        private static MulliganSelector _instance;

        protected override void Awake()
        {
            base.Awake();
            _instance = this;
            card_template.SetActive(false);
            Hide();
        }

        protected override void Update()
        {
            base.Update();
            if (!GameClient.Get().IsReady())
                return;
            
            timer += Time.deltaTime;
            
            foreach (CardSelectorCard card in card_img_list)
            {
                card.SetTargetPos(GetCardPos(card));
                card.selection_icon.sprite = card.deny_icon;

                if (card.selected)
                    card.SetTargetScale(Vector3.one / 1.1f);
                else
                    card.SetTargetScale(Vector3.one);
                //card.target_scale = card.selected ? Vector3.one / 1.1f : Vector3.one;
            }
            
            Game data = GameClient.Get().GetGameData();

            //Timer
            mulligan_timer.enabled = data.mulligan_timer > 0f;
            mulligan_timer.text = Mathf.RoundToInt(data.mulligan_timer).ToString();
            mulligan_timer.enabled = data.mulligan_timer < 999f;

            //Simulate timer
            if (data.state == GameState.Mulligan && data.mulligan_timer > 0f)
                data.mulligan_timer -= Time.deltaTime;
        }

        private void RefreshSelector()
        {
            Game data = GameClient.Get().GetGameData();

            foreach (CardSelectorCard card in card_img_list)
                Destroy(card.gameObject);
            card_img_list.Clear();
            //drag = false;
            //mouse_scroll = 0f;

            Card caster = data.GetCard(data.selector_caster_uid);

            int index = 0;
            int image_index = 0;
            foreach (Card card in mulligan_cards)
            {
                CardData icard = CardData.Get(card.card_id);
                //CardType card_type = iability.target_type;
                if (icard != null)
                {
                    if (iability == null || iability.AreTargetConditionsMet(data, caster, card))
                    {
                        GameObject card_obj = Instantiate(card_template, content.transform);
                        card_obj.SetActive(true);
                        RectTransform card_rect = card_obj.GetComponent<RectTransform>();
                        CardSelectorCard card_img = card_obj.GetComponent<CardSelectorCard>();
                        /*
                        //card_img.SetCard(index, image_index, card);
                        card_img.SetCard(card);
                        //card_img.target_pos = GetCardPos(card_img);
                        card_img.SetTargetPos(GetCardPos(card_img));
                        card_img.selection_text.GetComponent<RectTransform>().anchoredPosition = new Vector2(0,-300f);
                        card_img.selection_text.GetComponent<RectTransform>().sizeDelta = new Vector2(430f,100f);

                        //card_rect.anchoredPosition = card_img.target_pos;
                        card_rect.anchoredPosition = card_img.GetTargetPos();
                        */

                        card_img.SetCard(card);
                        card_img.SetIndex(index);
                        card_img.SetTargetPos(GetCardPos(card_img));
                        card_rect.anchoredPosition = card_img.GetTargetPos();

                        card_img_list.Add(card_img);
                        image_index++;
                    }
                }
                index++;
            }
        }

        private Vector2 GetCardPos(CardSelectorCard card)
        {
            int pos_index = card.GetIndex() - current_index;
            float card_space = ((RectTransform)card.gameObject.transform).rect.width;
            float posX = -(card_space + 100f) + (pos_index * card_space) + (pos_index * 100f);
            Vector2 pos = new Vector2(posX, 0f);
            //if (pos_index != 0)
            pos += Vector2.right * Mathf.Sign(pos_index);
            return pos;
        }

        private Vector2 GetMousePos()
        {
            Vector2 localpoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, Input.mousePosition, GetComponentInParent<Canvas>().worldCamera, out localpoint);
            //Vector2 normalizedPoint = Rect.PointToNormalized(content.rect, localpoint);
            return localpoint;
        }

        public void OnPointerDown()
        {
            mouse_start = GetMousePos();
            mouse_index_start = current_index;
            //drag = true;
        }

        public void OnPointerUp()
        {
            //drag = false;
            Vector2 mouse_pos = GetMousePos();
            Vector2 move = mouse_pos - mouse_start;
            if (move.magnitude < 2)
            {
                if (mouse_pos.x > 100f)
                    current_index += 1;
                else if (mouse_pos.x < -100f)
                    current_index -= 1;
                current_index = Mathf.Clamp(current_index, 0, card_img_list.Count - 1);
            }
        }

        public void OnClickOK()
        {
            Game data = GameClient.Get().GetGameData();
            title.text = "Waiting for opponent";
            //select_button.SetActive(false);
            select_image.sprite = after_select_icon;
            select_button_text.color = Color.white;
            List<Card> discardedCards = new List<Card>();
            foreach (CardSelectorCard card in card_img_list)
            {
                card.GetComponent<Button>().interactable = false;
                if (card.selected)
                {
                    discardedCards.Add(card.GetCard());
                }
            }
            
            GameClient.Get().PlayMulligan(discardedCards.ToArray());
        }

        public void OnClickCancel()
        {
            GameClient.Get().CancelSelection();
            Hide();
        }

        public void OnClickNext(int dir)
        {
            current_index += dir;
            current_index = Mathf.Clamp(current_index, 0, card_img_list.Count - 1);
        }

        public void Show(List<Card> card_list, AbilityData iability)
        {
            int cards_start = GameplayData.Get().cards_start;
            this.mulligan_cards = new List<Card>(card_list.GetRange(0, cards_start));
            this.iability = iability;
            title.text = iability.title;
            subtitle.text = iability.desc;
            current_index = 0;
            timer = 0f;
            Show();
            RefreshSelector();
        }

        public void Show(List<Card> card_list, string title)
        {
            int cards_start = GameplayData.Get().cards_start;
            this.mulligan_cards = new List<Card>(card_list.GetRange(0, cards_start));
            this.iability = null;
            this.title.text = title;
            subtitle.text = "";
            current_index = 0;
            timer = 0f;
            Show();
            RefreshSelector();
        }

        public bool IsAbility()
        {
            return IsVisible() && iability != null;
        }

        public static MulliganSelector Get()
        {
            return _instance;
        }
    }
}