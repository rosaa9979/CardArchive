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
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            Game game_data = logic.GetGameData();

            logic.MoveCard(game_data.GetCard(game_data.last_target), target);
        }
    }
}