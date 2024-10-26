using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that sets stats equal to a dynamic calculated value from a pile (number of cards on board/hand/deck)
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/DamageRatio", order = 10)]
    public class EffectDamageRatio : EffectData
    {
        public TraitData bonus_damage;
        public float ratio;


        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            int damage = Mathf.FloorToInt(target.hp * ratio);
            logic.DamagePlayer(caster, target, damage);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            int damage = Mathf.FloorToInt(target.GetHP() * ratio);

            logic.DamageCard(caster, target, damage, true);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            return;
        }
    }
}