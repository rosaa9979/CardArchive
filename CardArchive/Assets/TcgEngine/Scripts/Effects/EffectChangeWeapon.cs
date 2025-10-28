using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect change weapon
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/ChangeWeapon", order = 10)]
    public class EffectChangeWeapon : EffectData
    {
        public WeaponData weapon;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            target.SetWeapon(weapon.GetWeaponID());
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