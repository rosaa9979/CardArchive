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
            ResetAllFX(game_data);

            // Preview belongs to the selecting client, not whoever is hovering
            // while a remote player's selector is active.
            GameClient client = GameClient.Get();
            if (client == null || client.IsObserveMode() || game_data.selector != SelectorType.SelectTarget
                || !game_data.IsPlayerSelectorTurn(game_data.GetPlayer(client.GetPlayerID())))
                return;

            // On-play/activated/chained selectors must preview the ability that is
            // actually awaiting a target, not the caster's PlayTarget spell ability.
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);
            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            if (current_bslot == null || caster == null || ability == null
                || ability.criteria_target != AbilityTarget.SelectTarget)
                return;

            Slot selected = current_bslot.GetSlot();
            if (!ability.CanTarget(game_data, caster, selected, context: game_data.selector_context))
                return;

            foreach (BoardSlot board_slot in BoardSlot.GetAll())
            {
                Slot candidate = board_slot.GetSlot();
                if (ability.AreWideRangeConditionsMet(game_data, caster, selected, candidate, context: game_data.selector_context)
                    && ability.AreTargetConditionsMet(game_data, caster, candidate, context: game_data.selector_context))
                    board_slot.GetBoardSlotFX().SetRangeTarget(current_bslot, caster.uid, ability.id);
            }
        }

        public override bool RequireDim(Game game_data)
        {
            return false;
        }
    }
}
