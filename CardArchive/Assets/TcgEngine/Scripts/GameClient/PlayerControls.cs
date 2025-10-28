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

        private static PlayerControls instance;

        void Awake()
        {
            instance = this;
        }

        void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            if (Input.GetMouseButtonDown(1))
            {
                HandCard handcard = HandCard.GetDrag();
                if (handcard != null)
                {
                    handcard.SetFocus(false);
                    handcard.SetDrag(false);
                }

                UnselectAll();
            }

            if (selected_card != null)
            {
                if (Input.GetMouseButtonUp(0))
                    ReleaseClick();
            }

            Vector2 mouse_input = GameBoard.Get().RaycastMouseBoard();
            BSlot player_slot = BSlot.GetNearest(mouse_input);
            Player player = player_slot?.GetPlayer();
            if (player != null)
            {
                if (Input.GetMouseButtonDown(0))
                    SelectPlayer(player);
            }
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


        public void SelectCardRight(BoardCard card)
        {
            if (!Input.GetMouseButton(0))
            {
                //Nothing on right-click
            }
        }

        private void ReleaseClick()
        {
            bool yourturn = GameClient.Get().IsYourTurn();

            if (yourturn && selected_card != null)
            {
                Card card = selected_card.GetCard();
                Vector3 wpos = GameBoard.Get().RaycastMouseBoard();
                BSlot tslot = BSlot.GetNearest(wpos);
                Card target = tslot?.GetSlotCard(wpos);
                AbilityButton ability = AbilityButton.GetFocus(wpos, 1f);

                if (ability != null && ability.IsVisible())
                {
                    if (!Tutorial.Get().CanDo(TutoEndTrigger.CastAbility, card))
                        return;
                        
                    GameClient.Get().CastAbility(card, ability.GetAbility());
                }
            }

            UnselectAll();
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