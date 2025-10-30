using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine;


namespace TcgEngine.FX
{
    public class BSlotIndicatorTypeSelector : BSlotIndicatorType
    {
        public override void Execute(Game game_data, BSlot current_bslot)
        {
            ResetAllFX();

            AbilityData iability = AbilityData.Get(game_data.selector_ability_id);

            if (iability != null)
            {
                Card caster = game_data.GetCard(game_data.selector_caster_uid);

                if (current_bslot != null)
                {
                    List<BoardSlot> slots = BoardSlot.GetAll();
                    foreach (BoardSlot slot in slots)
                    {
                        if (iability.AreWideRangeConditionsMet(game_data, caster, current_bslot.GetSlot(), slot.GetSlot()))
                        {
                            BoardSlotFX fx = slot.GetBoardSlotFX();
                            fx?.SetAnimParameter(true);
                        }
                    }
                }
            }
        }

        public override bool RequireDim(Game game_data)
        {
            return false;
        }
    }
}
