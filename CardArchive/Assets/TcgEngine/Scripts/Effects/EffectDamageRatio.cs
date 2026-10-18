using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Deals ability damage proportional to the recipient's current health.
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/DamageRatio", order = 10)]
    public class EffectDamageRatio : EffectData
    {
        public TraitData bonus_damage;
        public float ratio;


        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            int damage = Mathf.FloorToInt(target.hp * ratio);
            logic.DamagePlayer_Event(caster, target, damage);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            int damage = Mathf.FloorToInt(target.GetHP() * ratio);

            logic.DamageCard_Event(caster, target, damage, true);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            logic.ApplyTileEffect(target,
                card => DoEffect(logic, ability, caster, card),
                player => DoEffect(logic, ability, caster, player));
        }
    }
}