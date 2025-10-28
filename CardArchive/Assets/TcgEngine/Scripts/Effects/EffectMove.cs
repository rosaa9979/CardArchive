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
            Game game_data = logic.GetGameData();
            Card target_unit = GetTarget(game_data, caster);
            logic.MoveCard(target_unit, target);
        }

        public Card GetTarget(Game gdata, Card caster)
        {
            if (target_type == EffectActionType.Self)
                return caster;
            if (target_type == EffectActionType.AbilityTriggerer)
                return gdata.GetCard(gdata.ability_triggerer);
            if (target_type == EffectActionType.LastPlayed)
                return gdata.GetCard(gdata.last_played);
            if (target_type == EffectActionType.LastTargeted)
                return gdata.GetSlotCard(gdata.last_targeted_slot);
            if (target_type == EffectActionType.LastSelected)
                return gdata.GetCard(gdata.last_selected);
            return null;
        }
    }
}