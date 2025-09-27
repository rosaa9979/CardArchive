using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;
using Unity.VisualScripting;
using System.Runtime.CompilerServices;
using TcgEngine.UI;
using TcgEngine.FX;

namespace TcgEngine.UI
{
    public enum TargetSelectorUIType
    {
        None = 0,
        AbilitySelector = 10,
        DragHandCard = 20,
        HoverBoardCard = 30
    }

    public class TargetSelectorUI : MonoBehaviour
    {
        public UIPanel ui_panel;
        public UnityAction<TargetSelectorUIType, BSlot> onCurrentSlotChanged;

        private BSlot prev_bslot = null;
        private List<BSlot> slot_list = new List<BSlot>();

        private static TargetSelectorUI _instance;

        public void Awake()
        {
            _instance = this;
        }

        public void Start()
        {
            slot_list = BSlot.GetAll();
        }

        public void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            TargetSelectorUIType current_type = TargetSelectorUIType.None;
            Game game_data = GameClient.Get().GetGameData();

            if (game_data.selector == SelectorType.SelectTarget && game_data.selector_player_id == GameClient.Get().GetPlayerID())
                current_type = TargetSelectorUIType.AbilitySelector;

            HandCard drag_card = HandCard.GetDrag();
            if (drag_card != null && drag_card.CardData.IsBoardCard())
                current_type = TargetSelectorUIType.DragHandCard;


            Vector3 board_pos = GameBoard.Get().RaycastMouseBoard();
            BSlot bslot = BSlot.GetNearest(board_pos);

            if (bslot != prev_bslot && current_type != TargetSelectorUIType.None)
            {
                prev_bslot = bslot;
                Execute(current_type, bslot);
                //onCurrentSlotChanged?.Invoke(current_type, current_slot);


            }

            //if (current_type != TargetSelectorUIType.None)
            //    ui_panel.Show();
            //else
            //    ui_panel.Hide();
        }

        public void Execute(TargetSelectorUIType type, BSlot current_bslot)
        {
            ResetUX();
            if (current_bslot == null || type == TargetSelectorUIType.None)
                return;

            List<BSlot> fx_list = new List<BSlot>();
            if (type == TargetSelectorUIType.DragHandCard)
            {
                Card hcard = HandCard.GetDrag().GetCard();
                Slot current_slot = current_bslot.GetSlot();

                if (hcard != null)
                {
                    List<Slot> range_slots = current_slot.GetNeighborSlot(hcard.GetRange());

                    foreach (BSlot bslot in slot_list)
                    {
                        Slot s = bslot.GetSlot();
                        if (range_slots.Contains(s))
                            fx_list.Add(bslot);
                    }
                }
            }

            PlayAnimationsCoroutine(fx_list, true);
        }

        public void ResetUX()
        {
            PlayAnimationsCoroutine(slot_list, false);
        }

        private IEnumerator PlayAnimationsCoroutine(List<BSlot> slots, bool parameter)
        {
            // 한 프레임 끝까지 대기 (모든 업데이트 완료 후)
            yield return new WaitForEndOfFrame();
            
            // 동시에 트리거
            foreach (BSlot slot in slots)
            {
                BoardSlotFX fx = slot.GetComponentInChildren<BoardSlotFX>();
                fx.SetAnimParameter(parameter);
            }
        }

        public static TargetSelectorUI Get()
        {
            return _instance;
        }
    }
}
