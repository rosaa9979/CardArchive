using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine;


namespace TcgEngine.FX
{
    public class BSlotIndicatorTypeNone : BSlotIndicatorType
    {
        public override void Execute(Game game_data, BSlot current_bslot)
        {
            Debug.Log("None");
            ResetAllFX();
        }

        public override bool RequireDim()
        {
            return false;
        }
    }
}
