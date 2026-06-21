using TcgEngine.Client;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.UI;
using DG.Tweening;
using Unity.VisualScripting;

namespace TcgEngine.Client
{
    /// <summary>
    /// Represents the visual aspect of a card in hand.
    /// Will take the data from Card.cs and display it
    /// </summary>

    public class HandCard : MonoBehaviour
    {
        public GameObject card_outline;
        public float move_speed = 10f;
        public float move_rotate_speed = 4f;
        public float move_max_rotate = 10f;

        [HideInInspector]
        public Vector2 deck_position;
        [HideInInspector]
        public float deck_angle;

        private string card_uid = "";

        [Header("HandCard")]
        public CardUI hand_card_ui;
        public CanvasGroup hand_canvas_group;

        [Header("BoardCard")]
        public CardUI board_card_ui;
        public CanvasGroup board_canvas_group;
        public Image board_card_image;

        private RectTransform hand_transform;
        private RectTransform card_transform;
        private Vector3 start_scale;
        private Vector3 current_rotate;
        private Vector3 target_rotate;
        private CanvasGroup canvas_group;

        private Vector3 prev_pos;

        private bool destroyed = false;
        private float focus_timer = 0f;

        private bool focus = false;
        private bool drag = false;
        private bool selected = false;
        private static bool select_target = false;

        private static List<HandCard> card_list = new List<HandCard>();

        private float default_move_speed;
        private bool auto_restore_speed = false;

        void Awake()
        {
            card_list.Add(this);
            card_transform = transform.GetComponent<RectTransform>();
            hand_transform = transform.parent.GetComponent<RectTransform>();
            canvas_group = GetComponent<CanvasGroup>();
            start_scale = transform.localScale;
            default_move_speed = move_speed;
        }

        private void Start()
        {

        }

        private void OnDestroy()
        {
            card_list.Remove(this);
        }

        void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            Game game_data = GameClient.Get().GetGameData();
            int player_id = GameClient.Get().GetPlayerID();
            Player player = game_data.GetPlayer(player_id);
            Card card = GetCard();
            Vector2 target_position = deck_position;
            Vector3 target_size = start_scale;

            focus_timer += Time.deltaTime;

            //Release hover focus when a target/choice selector is active — the hand isn't interactable
            //in that modal state, so a card hovered at the moment of entering selector mode must release.
            //Gated by `focus` so this runs once on entry (mirrors OnMouseExitCard).
            if (game_data.selector != SelectorType.None && focus)
            {
                focus = false;
                HandCardArea.Get().SortCards();
                focus_timer = -0.2f;
            }

            canvas_group.alpha = IsFocus() ? 0.0f : 1.0f;
            hand_canvas_group.alpha = 1;
            board_canvas_group.alpha = 0;

            if (IsFocus())
            {
                //target_position = target_position + Vector2.one * 40.0f;
            }

            else if (IsDrag())
            {
                target_position = GetTargetPosition();
                target_size = start_scale * 0.75f;
                Vector3 dir = card_transform.position - prev_pos;
                Vector3 addrot = new Vector3(dir.y * 90f, -dir.x * 90f, 0f);
                target_rotate += addrot * move_rotate_speed * Time.deltaTime;
                target_rotate = new Vector3(Mathf.Clamp(target_rotate.x, -move_max_rotate, move_max_rotate), Mathf.Clamp(target_rotate.y, -move_max_rotate, move_max_rotate), 0f);
                current_rotate = Vector3.Lerp(current_rotate, target_rotate, move_rotate_speed * Time.deltaTime);

                Vector3 mouse_pos = GameBoard.Get().RaycastMouseBoard();
                BSlot bslot = BSlot.GetNearest(mouse_pos);

                if (bslot != null && game_data.CanPlaceCard(GetCard(), bslot.GetSlot())
                    && GameClient.Get().IsYourTurn() && player.CanPayMana(GetCard()))
                {
                    hand_canvas_group.alpha = 0;
                    board_canvas_group.alpha = 1;
                }

                else
                {
                    hand_canvas_group.alpha = 1;
                    board_canvas_group.alpha = 0;
                }
            }
            else
            {
                target_rotate = new Vector3(0f, 0f, deck_angle);
                current_rotate = new Vector3(0f, 0f, deck_angle);
            }

            Vector2 mpos = GameCamera.Get().MouseToPercent(Input.mousePosition);
            if ((!GameClient.Get().IsYourTurn() || game_data.phase != GamePhase.Main) && IsDrag() && mpos.y >= 0.15f)
            {
                WarningText.ShowNotYourTurn();
                HandCardArea.Get().SortCards();
                drag = false;
                focus = false;
            }

            if (GameClient.Get().IsYourTurn() && IsDrag() && mpos.y >= 0.15f && !player.CanPayMana(card))
            {
                WarningText.ShowNoMana();
                HandCardArea.Get().SortCards();
                drag = false;
                focus = false;
            }

            card_transform.anchoredPosition = Vector2.Lerp(card_transform.anchoredPosition, target_position, Time.deltaTime * move_speed);
            card_transform.localRotation = Quaternion.Slerp(card_transform.localRotation, Quaternion.Euler(current_rotate), Time.deltaTime * move_speed);
            card_transform.localScale = Vector3.Lerp(card_transform.localScale, target_size, 5f * Time.deltaTime);

            //Once a temporary (e.g. mulligan handoff) move speed has carried the card home, restore the default
            if (auto_restore_speed && !IsDrag() && !IsFocus()
                && Vector2.Distance(card_transform.anchoredPosition, deck_position) < 1f)
            {
                move_speed = default_move_speed;
                auto_restore_speed = false;
            }

            hand_card_ui.SetCard(card);
            board_card_ui.SetCard(card);
            //card_glow.enabled = IsFocus() || IsDrag();
            bool is_outline_enabled = GameClient.Get().IsYourTurn() && game_data.phase == GamePhase.Main && game_data.CanPlay(GetCard());
            card_outline.SetActive(is_outline_enabled); 
            prev_pos = Vector3.Lerp(prev_pos, card_transform.position, 1f * Time.deltaTime);

            //Unselect
            if (!drag && selected && Input.GetMouseButtonDown(0))
                selected = false;
        }

        private Vector2 GetTargetPosition()
        {
            Card card = GetCard();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hand_transform, Input.mousePosition, Camera.main, out Vector2 tpos);
            if (card.CardData.IsRequireTarget())
            {
                tpos = deck_position + Vector2.up * 150f + Vector2.right * tpos.x / 10f;
            }

            if (IsDrag())
            {
                Game game_data = GameClient.Get().GetGameData();
                Vector3 mouse_pos = GameBoard.Get().RaycastMouseBoard();
                BSlot bslot = BSlot.GetNearest(mouse_pos);

                if (bslot != null && game_data.CanPlaceCard(card, bslot.GetSlot())
                    && GameClient.Get().IsYourTurn() && GameClient.Get().GetPlayer().CanPayMana(card))
                {
                    Vector3 screenPos = Camera.main.WorldToScreenPoint(bslot.transform.position);
            
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(hand_transform, screenPos, Camera.main, out tpos);
                }
            }
            return tpos;
        }

        public void SetCard(Card card)
        {
            this.card_uid = card.uid;
            hand_card_ui.SetCard(card);
            board_card_ui.SetCard(card);
            board_card_image.sprite = card.CardData.GetBoardArt(VariantData.GetDefault());
        }

        public void Kill()
        {
            if (!destroyed)
            {
                destroyed = true;
                Destroy(gameObject);
            }
        }

        public bool IsFocus()
        {
            //No hover/preview during game start and mulligan (cards may be gliding into hand here)
            Game game_data = GameClient.Get().GetGameData();
            if (game_data != null && (game_data.phase == GamePhase.GameStart || game_data.phase == GamePhase.Mulligan))
                return false;

            if (GameTool.IsMobile())
                return selected && !drag;
            return focus && !drag && focus_timer > 0f;
        }

        public bool IsDrag()
        {
            return drag;
        }

        public bool IsArrive()
        {
            return deck_position == card_transform.anchoredPosition;
        }

        public Card GetCard()
        {
            Game gdata = GameClient.Get().GetGameData();
            return gdata.GetCard(card_uid);
        }

        public CardData GetCardData()
        {
            Card card = GetCard();
            if (card != null)
                return CardData.Get(card.card_id);
            return null;
        }

        public string GetCardUID()
        {
            return card_uid;
        }

        public void OnMouseEnterCard()
        {
            if (GameUI.IsUIOpened())
                return;
            if (GameUI.Get().GetHideUI())
                return;

            focus = true;
        }

        public void OnMouseExitCard()
        {
            focus = false;
            HandCardArea.Get().SortCards();
            focus_timer = -0.2f;
        }

        public void OnMouseDownCard()
        {
            if (GameUI.IsOverUILayer("UI", 2))
                return;
            if (GameUI.Get().GetHideUI())
                return;

            UnselectAll();
            drag = true;
            selected = true;
            PlayerControls.Get().UnselectAll();
            AudioTool.Get().PlaySFX("hand_card", AssetData.Get().hand_card_click_audio);
        }

        public void OnMouseUpCard()
        {
            Vector2 mpos = GameCamera.Get().MouseToPercent(Input.mousePosition);
            Vector3 board_pos = GameBoard.Get().RaycastMouseBoard();

            if (drag && mpos.y > 0.15f)
                TryPlayCard(board_pos);
            else
                HandCardArea.Get().SortCards();
            drag = false;
            focus = false;
        }

        public void TryPlayCard(Vector3 board_pos)
        {
            if (!GameClient.Get().IsYourTurn())
            {
                WarningText.ShowNotYourTurn();
                return;
            }

            BSlot bslot = BSlot.GetNearest(board_pos);
            int player_id = GameClient.Get().GetPlayerID();
            Game gdata = GameClient.Get().GetGameData();
            Player player = gdata.GetPlayer(player_id);
            Card card = GetCard();

            Slot slot = Slot.None;
            if (bslot != null)
                slot = bslot.GetEmptySlot(board_pos);
            if (bslot != null && card.CardData.IsRequireTarget())
                slot = bslot.GetSlot(board_pos);

            if (!Tutorial.Get().CanDo(TutoEndTrigger.PlayCard, card, slot))
                return;
                
            Card slot_card = bslot?.GetSlotCard(board_pos);
            if (bslot != null && card.CardData.IsRequireTargetSpell() && slot_card != null && slot_card.HasStatus(StatusType.SpellImmunity))
            {
                WarningText.ShowSpellImmune();
                return;
            }

            if (!player.CanPayMana(card))
            {
                WarningText.ShowNoMana();
                return;
            }

            if (gdata.CanPlayCard(card, slot, true))
            {
                PlayCard(slot);
            }
        }

        public void PlayCard(Slot slot)
        {
            GameClient.Get().PlayCard(GetCard(), slot);
            HandCardArea.Get().DelayRefresh(GetCard());
            Destroy(gameObject);
            if (GameTool.IsMobile())
                BoardCard.UnfocusAll();
        }

        public void SetDrag(bool is_drag)
        {
            drag = is_drag;
        }

        public void SetFocus(bool is_focus)
        {
            focus = is_focus;
        }

        public void SetOpacity(float opacity)
        {
            hand_card_ui.SetOpacity(opacity);
            board_card_ui.SetOpacity(opacity);
        }

        public void SetHide(bool status)
        {
            this.gameObject.SetActive(status);
        }

        // General per-situation control over how fast the card lerps toward its hand slot.
        // restore_on_arrive: revert to the default (prefab) speed once the card reaches its slot.
        public void SetMoveSpeed(float speed, bool restore_on_arrive = false)
        {
            move_speed = speed;
            auto_restore_speed = restore_on_arrive;
        }

        public void ResetMoveSpeed()
        {
            move_speed = default_move_speed;
            auto_restore_speed = false;
        }

        // Places this card at the same screen position and size as the given mulligan card visual,
        // so the normal Update() lerp can then carry it into its hand slot (deck_position / start_scale).
        public void StartHandoffFrom(RectTransform source)
        {
            if (source == null)
                return;

            RectTransform rt = card_transform != null ? card_transform : (RectTransform)transform;
            RectTransform parent = hand_transform != null ? hand_transform : transform.parent as RectTransform;
            if (parent == null)
                return;

            Camera src_cam = GetCanvasCamera(source.GetComponentInParent<Canvas>());
            Camera dst_cam = GetCanvasCamera(GetComponentInParent<Canvas>());

            //Baseline scale so the world-height measurement below is consistent
            rt.localScale = start_scale;

            //Match the source card's on-screen size using the visible card UI as reference
            RectTransform my_vis = hand_card_ui != null ? (RectTransform)hand_card_ui.transform : rt;
            float src_h = source.rect.height * source.lossyScale.y;
            float my_h = my_vis.rect.height * my_vis.lossyScale.y;
            if (src_h > 0.0001f && my_h > 0.0001f)
                rt.localScale = start_scale * (src_h / my_h);

            //Match the source card's position (root center -> source center)
            Vector3 src_world = source.TransformPoint(source.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(src_cam, src_world);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, dst_cam, out Vector2 local))
                rt.anchoredPosition = local;

            focus = false;
            drag = false;
            if (canvas_group != null)
                canvas_group.alpha = 1f;
        }

        private Camera GetCanvasCamera(Canvas canvas)
        {
            if (canvas == null)
                return null;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;
            return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        public CardData CardData { get { return GetCardData(); } }

        public static HandCard GetDrag()
        {
            foreach (HandCard card in card_list)
            {
                if (card.IsDrag())
                    return card;
            }
            return null;
        }

        public static HandCard GetFocus()
        {
            foreach (HandCard card in card_list)
            {
                if (card.IsFocus())
                    return card;
            }
            return null;
        }

        public static HandCard Get(string uid)
        {
            foreach (HandCard card in card_list)
            {
                if (card && card.GetCardUID() == uid)
                    return card;
            }
            return null;
        }

        public static void UnselectAll()
        {
            foreach (HandCard card in card_list)
                card.selected = false;
        }

        public static bool GetSelect()
        {
            return select_target;
        }

        public static List<HandCard> GetAll()
        {
            return card_list;
        }
    }
}
