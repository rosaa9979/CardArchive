using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;
using Unity.VisualScripting;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/Destroy", order = 10)]
    public class EffectDestroy : EffectData
    {
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            if (logic.GameData.IsOnBoard(target))
                logic.KillCard(caster, target);
            else
                logic.DiscardCard(target);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            Game game_data = logic.GetGameData();
            Card slot_card = game_data.GetSlotCard(target);

            if (slot_card != null)
                DoEffect(logic, ability, caster, slot_card);
        }
    }
}