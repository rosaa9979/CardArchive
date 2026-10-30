using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect move unit to another slot
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/MoveUnit", order = 10)]
    public class EffectMoveUnit : EffectData
    {
        public EffectActionType target_type;
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            DoEffect(logic, ability, caster, target, null);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target, AbilityEventContext context)
        {
            Game game_data = logic.GetGameData();
            Card target_unit = GetTarget(game_data, caster, context);
            logic.MoveCard(target_unit, target, true); //Forced relocation: not recorded as a player move
        }

        //Move selection: the unit on the departure slot moves to the destination slot (target_type is not used)
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot selected, Slot target)
        {
            Game game_data = logic.GetGameData();
            Card target_unit = game_data.GetSlotCard(selected);
            logic.MoveCard(target_unit, target, true);
        }

        public Card GetTarget(Game gdata, Card caster, AbilityEventContext context = null)
        {
            if (target_type == EffectActionType.Self)
                return caster;
            if (target_type == EffectActionType.AbilityTriggerer)
                return gdata.GetCard(((context as AbilityExecutionContext)?.trigger_uid ?? gdata.ability_triggerer));
            if (target_type == EffectActionType.LastPlayed)
                return gdata.GetCard(((context as AbilityExecutionContext)?.last_played ?? gdata.last_played));
            if (target_type == EffectActionType.LastTargeted)
                return context is AbilityExecutionContext execution
                    ? gdata.GetCard(execution.reference_target_uid)
                    : gdata.GetSlotCard(gdata.last_targeted_slot);
            if (target_type == EffectActionType.LastSelected)
                return gdata.GetCard(((context as AbilityExecutionContext)?.last_selected ?? gdata.last_selected));
            return null;
        }
    }
}