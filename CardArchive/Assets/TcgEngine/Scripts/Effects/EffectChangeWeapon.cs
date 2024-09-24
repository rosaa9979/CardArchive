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
    }
}