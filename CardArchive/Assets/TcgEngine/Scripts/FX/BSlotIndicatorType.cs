using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using TcgEngine.Client;
using UnityEngine;

namespace TcgEngine.FX
{
    public class BSlotIndicatorType
    {
        public virtual List<BSlot> GetTargetBSlot(BSlot current_bslot)
        {
            return new List<BSlot>();
        }
        public virtual void Execute(Game game_data, BSlot current_bslot)
        {
            // 자식에서 구현
        }

        public virtual bool RequireDim()
        {
            return false;
        }

        public void ResetAllFX()
        {
            foreach (BoardSlot board_slot in BoardSlot.GetAll())
            {
                
                if (board_slot.GetBoardSlotFX() != null)
                {
                    board_slot.GetBoardSlotFX().ResetIndicator();
                }

            }
        }
    }
}