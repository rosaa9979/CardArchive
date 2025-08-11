using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using TcgEngine.Client;
using TcgEngine.UI;
using Unity.VisualScripting;
using UnityEngine.AI;

namespace TcgEngine.Client
{
    /// <summary>
    /// Visual representation of a Slot.cs
    /// Will highlight when can be interacted with
    /// </summary>

    public class BoardSlot : BSlot
    {
        public BoardSlotType type;
        public int x;
        public int y;
        public GameObject attachment;

        public SlotSelectorPanel select_panel;
        public SpriteRenderer overlay_renderer;

        private static List<BoardSlot> slot_list = new List<BoardSlot>();

        protected override void Awake()
        {
            base.Awake();
            slot_list.Add(this);

            select_panel.onSlotSelectedByCard += OnSelectedByDragCard;
            select_panel.onSlotSelectedByBoardCard += OnSelectedByBoardCard;
            select_panel.onSlotSelectedByAbility += OnSelectedByAbility;
            select_panel.onSlotSelectedClear += OnSelectedClear;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            slot_list.Remove(this);

            select_panel.onSlotSelectedByCard -= OnSelectedByDragCard;
            select_panel.onSlotSelectedByBoardCard -= OnSelectedByBoardCard;
            select_panel.onSlotSelectedByAbility -= OnSelectedByAbility;
            select_panel.onSlotSelectedClear -= OnSelectedClear;
        }

        private void Start()
        {
            if (x < Slot.x_min || x > Slot.x_max || y < Slot.y_min || y > Slot.y_max)
                Debug.LogError("Board Slot X and Y value must be within the min and max set for those values, check Slot.cs script to change those min/max.");
        }

        protected override void Update()
        {
            // 매 프레임마다 플레이어의 행동에 따라 투명도를 계산해서 반영

            base.Update();

            if (!GameClient.Get().IsReady())
                return;

            BoardCard bcard_selected = PlayerControls.Get().GetSelected();
            HandCard drag_card = HandCard.GetDrag();

            Game gdata = GameClient.Get().GetGameData();
            Player player = GameClient.Get().GetPlayer();
            Slot slot = GetSlot();
            Card dcard = drag_card?.GetCard();
            Card slot_card = gdata.GetSlotCard(GetSlot());
            bool your_turn = GameClient.Get().IsYourTurn();
            collide.enabled = slot_card == null; //Disable collider when a card is here

            if (gdata.GetAttachCard(GetSlot()) != null)
                attachment.SetActive(true);
            else
                attachment.SetActive(false);

            target_alpha = 1f;

            //Find target opacity value
            /*
            if (drag_card != null)
            {
                //target_alpha = 1f;
                if (your_turn && dcard != null && dcard.CardData.IsBoardCard() && (!gdata.CanPlayCard(dcard, slot)))
                {
                    target_alpha = 0f; //hightlight when dragging a citizen or building
                }

                if (your_turn && dcard != null && dcard.CardData.IsRequireTarget() && gdata.CanPlayCard(dcard, slot))
                {
                    target_alpha = 1f; //Highlight when dragin a spell with target
                }

                if (gdata.selector == SelectorType.SelectTarget && player.player_id == gdata.selector_player_id)
                {
                    Card caster = gdata.GetCard(gdata.selector_caster_uid);
                    AbilityData ability = AbilityData.Get(gdata.selector_ability_id);
                    if (ability != null && slot_card == null && ability.CanTarget(gdata, caster, slot))
                        target_alpha = 1f; //Highlight when selecting a target and slot are valid
                    if (ability != null && slot_card != null && ability.CanTarget(gdata, caster, slot_card))
                        target_alpha = 1f; //Highlight when selecting a target and cards are valid
                }


                Card select_card = bcard_selected?.GetCard();
                bool can_do_move = your_turn && select_card != null && slot_card == null && gdata.CanMoveCard(select_card, slot);
                bool can_do_attack = your_turn && select_card != null && slot_card != null && gdata.CanAttackTarget(select_card, slot_card);

                if (can_do_attack || can_do_move)
                {
                    target_alpha = 1f;
                }
            }
            */

        }

        //Find the actual slot coordinates of this board slot
        public override Slot GetSlot()
        {
            int p = GameClient.Get().GetPlayerID();
            int new_x = x;
            int new_y = y;

            if (type == BoardSlotType.FlipX)
            {
                int pid = GameClient.Get().GetPlayerID();
                int px = x;
                if ((pid % 2) == 1)
                    px = Slot.x_max - x + Slot.x_min; //Flip X coordinate if not the first player
                return new Slot(px, y, p);
            }

            if (type == BoardSlotType.FlipY)
            {
                int pid = GameClient.Get().GetPlayerID();
                int py = y;
                if ((pid % 2) == 1)
                    py = Slot.y_max - y + Slot.y_min; //Flip Y coordinate if not the first player
                return new Slot(x, py, p);
            }

            /*
            int pid = GameClient.Get().GetPlayerID();
            int new_x = x;
            int new_y = y;

            if (pid % 2 == 1)
            {
                new_y = Slot.y_max - y + 1;
            }

            if (type == BoardSlotType.PlayerSelf || type == BoardSlotType.PlayerField)
                p = GameClient.Get().GetPlayerID();
            if (type == BoardSlotType.OpponentSelf || type == BoardSlotType.OpponentField)
                p = GameClient.Get().GetOpponentPlayerID();
            if (type == BoardSlotType.Neutral)
                p = 2;
            */

            int new_p = p;

            if (y <= (int)Math.Floor(((double)Slot.y_max / 2)))
                new_p = GameClient.Get().GetPlayerID();
            else if (y > (int)Math.Ceiling(((double)Slot.y_max / 2)))
                new_p = GameClient.Get().GetOpponentPlayerID();
            else if (y == (int)Math.Ceiling(((double)Slot.y_max / 2)))
                new_p = 2;


            if (p % 2 == 1)
            {
                new_x = Slot.x_min + Slot.x_max - x;
                new_y = y + 2 * ((Slot.y_max / 2 + 1) - y);
            }


            return new Slot(new_x, new_y, new_p);
        }

        public void OnSelectedByDragCard(Card card, Slot selected_slot)
        {
            OnSelectedClear();

            Game game_data = GameClient.Get().GetGameData();

            if (game_data != null)
            {
                if (card != null)
                {
                    if (game_data.CanPlaceCard(card, GetSlot()))
                    {
                        SetSelected(true);
                    }

                    else
                    {
                        SetSelected(false);
                    }
                }

                else
                {
                    SetSelected(false);
                }
            }

            if (!selected_slot.IsValid())
                return;

            List<Slot> range_slots = new List<Slot>();

            if (game_data.CanPlaceCard(card, selected_slot))
            {
                range_slots = selected_slot.GetNeighborSlot(card.GetRange());

                foreach (Slot slot in range_slots)
                {
                    if (GetSlot() == slot && GetSlot() != selected_slot)
                    {
                        SetSelected(true);

                        if (overlay_renderer)
                        {
                            overlay_renderer.color = new Color(Color.red.r, Color.red.g, Color.red.b, 0.3f);
                        }
                    }
                }
            }
        }

        public void OnSelectedByBoardCard(Card card, Slot selected_slot)
        {
            OnSelectedClear();

            Game game_data = GameClient.Get().GetGameData();

            if (!selected_slot.IsValid())
                return;

            List<Slot> range_slots = selected_slot.GetNeighborSlot(card.GetRange());

            foreach (Slot slot in range_slots)
            {
                if (GetSlot() == slot && GetSlot() != selected_slot)
                {
                    SetSelected(true);

                    if (overlay_renderer)
                    {
                        overlay_renderer.color = new Color(Color.red.r, Color.red.g, Color.red.b, 0.3f);
                    }
                }
            }
        }

        public void OnSelectedByAbility(AbilityData ability, Slot selected_slot)
        {
            OnSelectedClear();

            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            Game game_data = GameClient.Get().GetGameData();
            Card caster = game_data.GetCard(game_data.selector_caster_uid);

            if (!selected_slot.IsValid())
                return;
                
            if (game_data != null)
            {
                if (ability.CanTarget(game_data, caster, GetSlot()) || ability.CanTarget(game_data, caster, game_data.GetSlotCard(GetSlot())))
                {
                    //renderer.sortingOrder = 20;
                    SetSelected(true);

                    if (ability.condition_wide_range != null && ability.condition_wide_range.IsTargetConditionMet(game_data, ability, caster, selected_slot, GetSlot()))
                    {
                        SetSelected(true);

                        if (overlay_renderer)
                            overlay_renderer.color = new Color(Color.red.r, Color.red.g, Color.red.b, 0.3f);
                    }
                }

                else
                {
                    //renderer.sortingOrder = 0;
                    SetSelected(false);
                }
            }
        }

        public void OnSelectedClear()
        {
            //renderer.sortingOrder = 0;
            SetSelected(false);
            overlay_renderer.color = new Color(overlay_renderer.color.r, overlay_renderer.color.g, overlay_renderer.color.b, 0);
        }

        //When clicking on the slot
        public void OnMouseDown()
        {
            if (GameUI.IsOverUI())
                return;

            GameClient.Get().SelectSlot(GetSlot());
        }

        private void SetSelected(bool is_selected)
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();

            renderer.sortingLayerName = is_selected ? "BoardSelectorUI" : "Default";
            overlay_renderer.sortingLayerName = is_selected ? "BoardSelectorUI" : "Default";

            Game game_data = GameClient.Get().GetGameData();
            Card slot_card = game_data.GetSlotCard(GetSlot());
            if (slot_card != null)
            {
                BoardCard board_card = BoardCard.Get(slot_card.uid);
                board_card.SetSelected(is_selected);
            }
        }
    }
}