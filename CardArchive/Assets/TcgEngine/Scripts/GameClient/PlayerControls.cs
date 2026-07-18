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

        private static PlayerControls instance;

        void Awake()
        {
            instance = this;
        }

        void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            //Right-click cancel (PC only, mobile uses the cancel button / release rules)
            if (Input.GetMouseButtonDown(1))
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

            //Press starts on the board only (not over UI, not on a hand card)
            if (Input.GetMouseButtonDown(0) && !press_active)
            {
                bool blocked = GameUI.IsUIOpened() || GameUI.IsOverUILayer("UI")
                    || HandCard.GetPressed() != null || HandCardArea.Get().IsDragging();
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
            Game gdata = GameClient.Get().GetGameData();
            Player player = GameClient.Get().GetPlayer();
            Card card = bcard.GetFocusCard();

            if (gdata.IsPlayerSelectorTurn(player) && gdata.selector == SelectorType.SelectTarget)
            {
                if (!Tutorial.Get().CanDo(TutoEndTrigger.SelectTarget, card))
                    return;

                //Target selector, select this card
                GameClient.Get().SelectSlot(card.slot);
            }
            else if (gdata.IsPlayerActionTurn(player) && card.player_id == player.player_id)
            {
                //Start ging card
                selected_card = bcard;
            }
        }

        public void SelectSlot(BoardSlot bslot)
        {
            Game gdata = GameClient.Get().GetGameData();
            Player player = GameClient.Get().GetPlayer();
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
            Game gdata = GameClient.Get().GetGameData();
            Player client_player = GameClient.Get().GetPlayer();

            if (gdata.IsPlayerSelectorTurn(client_player) && gdata.selector == SelectorType.SelectTarget)
            {
                GameClient.Get().SelectPlayer(player);
            }
        }


        public void UnselectAll()
        {
            selected_card = null;

            Game gdata = GameClient.Get().GetGameData();
            HandCard handcard = HandCard.GetDrag();

            if (gdata.selector == SelectorType.SelectTarget)
            {
                AbilityData iability = AbilityData.Get(gdata.selector_ability_id);

                if (iability != null && iability.can_cancel)
                {
                    GameClient.Get().CancelSelection();
                }
            }
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