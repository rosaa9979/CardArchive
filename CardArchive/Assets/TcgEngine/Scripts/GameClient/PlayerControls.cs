using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Client;
using UnityEngine.Events;
using TcgEngine.UI;
using System.Linq;

namespace TcgEngine.Client
{
    /// <summary>
    /// Script that contain main controls for clicking on cards, attacking, activating abilities
    /// Holds the currently selected card and will send action to GameClient on click release
    /// </summary>

    public class PlayerControls : MonoBehaviour
    {
        private BoardCard selected_card = null;

        //Board press gesture: hold to preview (scrub), release to confirm
        private bool press_active = false;
        private bool press_long = false;
        private float press_time = 0f;

        //Move selection (SelectorType.SelectMove): drag a unit to a slot, sent only by ConfirmMoveSelection
        private bool move_dragging = false;
        private Slot move_from = Slot.None;
        private Slot move_to = Slot.None;
        private long move_version = -1;

        private static PlayerControls instance;

        void Awake()
        {
            instance = this;
        }

        void Update()
        {
            if (TcgEngine.Replay.ReplaySession.Active) return;
            if (!GameClient.Get().IsReady())
                return;

            //Right-click cancel (PC only, mobile uses the cancel button / release rules)
            if (!GameTool.IsMobile() && Input.GetMouseButtonDown(1))
            {
                HandCard handcard = HandCard.GetDrag();
                if (handcard != null)
                {
                    handcard.SetFocus(false);
                    handcard.SetDrag(false);
                }

                HandCard.CancelPress();
                CancelBoardPress();
                UnselectAll();
            }

            //Move selection owns the board press while it is open
            if (UpdateMoveSelection())
                return;

            //Press starts on the board only (not over UI, not on a hand card)
            if (Input.GetMouseButtonDown(0) && !press_active)
            {
                bool blocked = GameUI.IsUIOpened() || GameUI.IsOverUILayer("UI")
                    || HandCard.GetPressed() != null || HandCard.GetDrag() != null
                    || HandCardArea.Get().IsDragging();
                if (!blocked)
                {
                    press_active = true;
                    press_long = false;
                    press_time = 0f;
                    UpdateBoardFocus();
                }
            }

            if (press_active && Input.GetMouseButton(0))
            {
                press_time += Time.deltaTime;
                if (press_time >= GameConfig.Gesture.preview_delay)
                    press_long = true; //Preview gesture: release will not confirm anything

                UpdateBoardFocus();
            }

            if (press_active && Input.GetMouseButtonUp(0))
            {
                press_active = false;

                if (!press_long)
                    ConfirmRelease();

                if (GameTool.IsMobile())
                    BoardCard.UnfocusAll(); //No hover on touch: close the preview on release
            }
        }

        //Focus (preview/status bar) follows the pointer while pressed; empty space clears it
        private void UpdateBoardFocus()
        {
            Vector3 wpos = GameBoard.Get().RaycastMouseBoard();
            BSlot bslot = BSlot.GetNearest(wpos);
            Card slot_card = bslot?.GetSlotCard(wpos);
            BoardCard bcard = slot_card != null ? BoardCard.Get(slot_card.uid) : null;

            if (bcard != BoardCard.GetFocus())
            {
                BoardCard.UnfocusAll();
                if (bcard != null)
                    bcard.SetFocus();
            }
        }

        //Tap released: confirm whatever is under the pointer at release
        private void ConfirmRelease()
        {
            Vector3 wpos = GameBoard.Get().RaycastMouseBoard();
            BSlot bslot = BSlot.GetNearest(wpos);
            if (bslot == null)
                return;

            Card slot_card = bslot.GetSlotCard(wpos);
            if (slot_card != null)
            {
                BoardCard bcard = BoardCard.Get(slot_card.uid);
                if (bcard != null)
                    SelectCard(bcard);
            }
            else if (bslot.GetPlayer() != null)
            {
                SelectPlayer(bslot.GetPlayer());
            }
            else if (bslot is BoardSlot board_slot)
            {
                SelectSlot(board_slot);
            }
        }

        private bool UpdateMoveSelection()
        {
            GameClient client = GameClient.Get();
            Game gdata = client.GetLatestGameData();
            Player player = gdata.GetPlayer(client.GetPlayerID());
            if (gdata.selector != SelectorType.SelectMove || !gdata.IsPlayerSelectorTurn(player))
            {
                ClearMoveSelection();
                return false;
            }

            //A new selection never reuses the pair chosen for a previous one
            if (move_version != gdata.selector_version)
            {
                ClearMoveSelection();
                move_version = gdata.selector_version;
            }

            CancelBoardPress();

            if (Input.GetMouseButtonDown(0) && client.IsInputOpen && !GameUI.IsUIOpened() && !SelectMoveUI.IsPointerOver())
            {
                //Dragging again always clears the previous pair
                move_from = Slot.None;
                move_to = Slot.None;
                BSlot bslot = BSlot.GetNearest(GameBoard.Get().RaycastMouseBoard());
                Card unit = bslot is BoardSlot ? gdata.GetSlotCard(bslot.GetSlot()) : null;
                if (unit != null && gdata.IsOnBoard(unit))
                {
                    move_from = unit.slot;
                    move_dragging = true;
                }
            }

            if (move_dragging && Input.GetMouseButtonUp(0))
            {
                move_dragging = false;
                BSlot bslot = BSlot.GetNearest(GameBoard.Get().RaycastMouseBoard());
                Slot to = bslot is BoardSlot ? bslot.GetSlot() : Slot.None;
                if (IsValidMove(gdata, move_from, to))
                    move_to = to;
                else
                    move_from = Slot.None; //Released on an invalid slot: no pair
            }
            return true;
        }

        private bool IsValidMove(Game gdata, Slot from, Slot to)
        {
            AbilityData ability = AbilityData.Get(gdata.selector_ability_id);
            Card caster = gdata.GetCard(gdata.selector_caster_uid);
            return ability != null && ability.CanTarget(gdata, caster, from, to, gdata.selector_context);
        }

        public bool IsMoveDragging()
        {
            return move_dragging;
        }

        public Slot GetMoveFrom()
        {
            return move_from;
        }

        public Slot GetMoveTo()
        {
            return move_to;
        }

        //A complete pair that is still valid on the latest state
        public bool HasMoveSelection()
        {
            Game gdata = GameClient.Get().GetLatestGameData();
            return !move_dragging && move_to != Slot.None && gdata.selector == SelectorType.SelectMove
                && move_version == gdata.selector_version && IsValidMove(gdata, move_from, move_to);
        }

        public void ConfirmMoveSelection()
        {
            if (!HasMoveSelection())
                return;
            GameClient.Get().SelectMove(move_from, move_to);
            ClearMoveSelection();
        }

        public void ClearMoveSelection()
        {
            move_dragging = false;
            move_from = Slot.None;
            move_to = Slot.None;
        }

        private void CancelBoardPress()
        {
            press_active = false;
            press_long = false;
        }

        public bool IsPressActive()
        {
            return press_active;
        }

        public void SelectCard(BoardCard bcard)
        {
            Game gdata = GameClient.Get().GetLatestGameData();
            Player player = GameClient.Get().GetLatestGameData().GetPlayer(GameClient.Get().GetPlayerID());
            Card visible = bcard.GetFocusCard();
            if (visible == null) return;
            if (gdata.IsPlayerSelectorTurn(player) && gdata.IsSlotSelection())
            {
                if (!Tutorial.Get().CanDo(TutoEndTrigger.SelectTarget, visible.slot)) return;
                GameClient.Get().SelectSlot(visible.slot);
                return;
            }
            if (!GameClient.Get().CanUseBoardCard(visible.uid)) return;
            Card card = gdata.GetCard(visible.uid);
            if (gdata.IsPlayerActionTurn(player) && card.player_id == player.player_id)
                selected_card = bcard;
        }

        public void SelectSlot(BoardSlot bslot)
        {
            Game gdata = GameClient.Get().GetLatestGameData();
            Player player = GameClient.Get().GetLatestGameData().GetPlayer(GameClient.Get().GetPlayerID());
            Slot slot = bslot.GetSlot();

            if (gdata.IsPlayerSelectorTurn(player) && gdata.selector == SelectorType.SelectTarget)
            {
                if (!Tutorial.Get().CanDo(TutoEndTrigger.SelectTarget, slot))
                    return;

                //Target selector, select this card
                GameClient.Get().SelectSlot(slot);
            }
        }

        public void SelectPlayer(Player player)
        {
            Game gdata = GameClient.Get().GetLatestGameData();
            Player client_player = GameClient.Get().GetLatestGameData().GetPlayer(GameClient.Get().GetPlayerID());

            if (gdata.IsPlayerSelectorTurn(client_player) && gdata.selector == SelectorType.SelectTarget)
            {
                GameClient.Get().SelectPlayer(player);
            }
        }


        public void UnselectAll()
        {
            selected_card = null;
            ClearMoveSelection();

            Game gdata = GameClient.Get().GetLatestGameData();
            HandCard handcard = HandCard.GetDrag();

            if (gdata.CanCancelSelector())
                GameClient.Get().CancelSelection();
        }

        public BoardCard GetSelected()
        {
            return selected_card;
        }

        public static PlayerControls Get()
        {
            return instance;
        }
    }
}