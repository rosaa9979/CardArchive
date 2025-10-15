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
            ResetAllFX();
        }

        public override bool RequireDim(Game game_data)
        {
            return false;
        }
    }
}
