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

            Card card = game_data.GetCard(game_data.selector_caster_uid);
            AbilityData iability = AbilityData.Get(game_data.selector_ability_id);

            if (iability != null)
            {
                Card caster = game_data.GetCard(game_data.selector_caster_uid);

                if (current_bslot != null)
                {
                   AbilityData ability = card.GetAbility(AbilityTarget.PlayTarget);

                    if (ability != null && ability.CanTarget(game_data, card, current_bslot.GetSlot()))
                    {
                        foreach (BoardSlot board_slot in BoardSlot.GetAll())
                        {
                            if (ability.AreWideRangeConditionsMet(game_data, card, current_bslot.GetSlot(), board_slot.GetSlot()) && ability.AreTargetConditionsMet(game_data, card, board_slot.GetSlot()))
                            {
                                BoardSlotFX fx = board_slot.GetBoardSlotFX();
                                fx.SetAnimParameter(true);
                            }
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
