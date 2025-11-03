using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine;
using DG.Tweening;

namespace TcgEngine.UI
{
    /// <summary>
    /// The UI for card selector, appears when an ability with CardSelector target is triggered
    /// </summary>

    public class CardSelector : SelectorPanel
    {
        public GameObject card_prefab;

        public RectTransform content;
        public Text title;
        public Text subtitle;

        public Button select_button;
        public Text select_button_text;
        public float card_spacing = 100f;

        private AbilityData iability;
        private CardSelectorCard current_card;

        private List<Card> card_list = new List<Card>();
        private List<CardSelectorCard> selector_list = new List<CardSelectorCard>();

        private float interval;
        //private int selection_index = 0;
        //private float timer = 0f;

        private static CardSelector instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
            Hide();
        }

        /*
        protected override void Update()
        {
            base.Update();

            timer += Time.deltaTime;

            //Drag cards
            Vector2 mouse_pos = GetMouseRectPosition();
            Vector2 move = mouse_pos - mouse_start;
            if (drag && move.magnitude > 0.1f)
            {
                selection_index = mouse_start_index - Mathf.RoundToInt(move.x / card_spacing);
                selection_index = Mathf.Clamp(selection_index, 0, selector_list.Count - 1);
            }

            
            //Mouse scroll
            mouse_scroll += -Input.mouseScrollDelta.y;
            if (mouse_scroll > 0.5f)
            {
                OnClickNext();
                mouse_scroll -= 1f;
            }
            else if (mouse_scroll < -0.5f)
            {
                OnClickPrev();
                mouse_scroll += 1f;
            }
            

            //Refresh cards
            foreach (CardSelectorCard card in selector_list)
            {
                bool is_selected = card.GetIndex() == selection_index;
                Vector3 pos = GetCardPosition(card);
                Vector3 scale = is_selected ? Vector3.one : Vector3.one / 2f;
                card.SetTargetPos(pos);
                card.SetTargetScale(scale);
            }

            //Close on right click if not a selection
            if (iability == null && Input.GetMouseButtonDown(1) && timer > 1f)
                Hide();

            Game game = GameClient.Get().GetGameData();
            if (game != null && iability != null && game.selector == SelectorType.None)
                Hide(); //Ability was selected already, close panel
        }
        */

        public void RefreshPanel()
        {
            foreach (CardSelectorCard card in selector_list)
                Destroy(card.gameObject);
            selector_list.Clear();

            //elect_button_text.text = (iability != null) ? "선택 완료" : "선택 완료";
            //select_button.gameObject.SetActive(iability != null);

            interval = content.rect.width / (card_list.Count + 1);
            int index = 0;
            foreach (Card card in card_list)
            {
                CardData icard = CardData.Get(card.card_id);
                if (icard != null)
                {
                    GameObject obj = Instantiate(card_prefab, content.transform);

                    RectTransform rect = obj.GetComponent<RectTransform>();
                    CardSelectorCard selector_card = obj.GetComponent<CardSelectorCard>();
                    selector_card.SetCard(card);
                    selector_card.gameObject.SetActive(true);
                    selector_card.SetIndex(index);
                    selector_card.onClick += OnClickCard;

                    Vector3 pos = GetCardPosition(selector_card);
                    selector_card.SetTargetPos(pos);
                    rect.anchoredPosition = pos;
                    selector_card.SetTargetScale(Vector3.one);
                    selector_card.DoShow();
                    selector_list.Add(selector_card);

                    index++;
                }
            }
        }

        //Show ability
        public override void Show(AbilityData iability, Card caster)
        {
            Game data = GameClient.Get().GetGameData();
            this.card_list = iability.GetCardTargets(data, caster);
            this.iability = iability;
            this.current_card = null;
            title.text = iability.title;
            subtitle.text = iability.desc;
            //selection_index = 0;
            //timer = 0f;
            Show();
        }

        //Show deck/discard
        public void Show(List<Card> card_list, string title)
        {
            this.card_list.Clear();
            this.card_list.AddRange(card_list);
            this.card_list.Sort((Card a, Card b) => { return a.CardData.title.CompareTo(b.CardData.title); }); //Reorder to not show the deck order
            this.iability = null;
            this.current_card = null;
            this.title.text = title;
            subtitle.text = "";
            //selection_index = 0;
            //timer = 0f;
            Show();
        }

        public async void OnClickOK(CardSelectorCard selector_card)
        {
            Game data = GameClient.Get().GetGameData();
            if (iability != null && data.selector == SelectorType.SelectorCard)
            {
                if (selector_card != null)
                {
                    Card selected_card = selector_card.GetCard();
                    Card caster = data.GetCard(data.selector_caster_uid);
                    if (selected_card != null && iability.AreCriteriaTargetConditionsMet(data, caster, selected_card))
                    {
                        current_card = selector_card;
                        await AfterSelectAsync();
                        GameClient.Get().SelectCard(selected_card);
                        Hide();
                        return;
                    }
                }
            }
            Hide();
        }

        public async Task AfterSelectAsync()
        {
            List<Task> animTasks = new List<Task>();
            
            foreach (CardSelectorCard card in selector_list)
            {
                if (current_card == card)
                    animTasks.Add(card.DoHideAsync(true));  // DoHide를 async로 변경

                else
                    animTasks.Add(card.DoHideAsync());
            }
            
            // 모든 애니메이션 완료 대기
            await Task.WhenAll(animTasks);
        }

        public void OnClickCard(CardSelectorCard selector_card)
        {
            OnClickOK(selector_card);
        }

        /*
        public void OnClickMouseDown()
        {
            mouse_start = GetMouseRectPosition();
            mouse_start_index = selection_index;
            drag = true;
        }

        public void OnClickMouseUp()
        {
            drag = false;
        }

        public void OnClickCancel()
        {
            GameClient.Get().CancelSelection();
            Hide();
        }

        public void OnClickNext()
        {
            selection_index += 1;
            selection_index = Mathf.Clamp(selection_index, 0, selector_list.Count - 1);
        }

        public void OnClickPrev()
        {
            selection_index -= 1;
            selection_index = Mathf.Clamp(selection_index, 0, selector_list.Count - 1);
        }
        */

        private Vector2 GetCardPosition(CardSelectorCard card)
        {
            /*
            int index_offset = card.GetIndex() - selection_index;
            Vector2 pos = new Vector2(index_offset * card_spacing, (index_offset != 0) ? 50f : 0f);
            float center_offset = (index_offset != 0) ? (Mathf.Sign(index_offset) * 140f) : 0;
            pos += Vector2.right * center_offset;
            return pos;
            */

            float xPos = interval * (card.GetIndex()+1) - (content.rect.width / 2);
            Vector2 position = new Vector2(xPos, 0);
            return position;
        }

        private Vector2 GetMouseRectPosition()
        {
            Vector2 localpoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, Input.mousePosition, GetComponentInParent<Canvas>().worldCamera, out localpoint);
            return localpoint;
        }

        public bool IsAbility()
        {
            return IsVisible() && iability != null;
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);
            RefreshPanel();
        }

        public override bool ShouldShow()
        {
            Game data = GameClient.Get().GetGameData();
            int player_id = GameClient.Get().GetPlayerID();
            return data.selector == SelectorType.SelectorCard && data.selector_player_id == player_id;
        }

        public static CardSelector Get()
        {
            return instance;
        }
    }
}