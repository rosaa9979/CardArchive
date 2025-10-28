using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that damages a card or a player (lose hp)
    /// </summary>

    public enum EffectValueType
    {
        Value = 0,
        Attack = 1,
        Health = 2,
    }

    public enum EffectDamageType
    {
        Card = 0,
        Slot = 1,
    }


    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/Damage", order = 10)]
    public class EffectDamage : EffectData
    {
        public EffectValueType damage_type;
        public EffectValueType value_type;
        public TraitData bonus_damage;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            int damage = GetDamage(logic.GameData, caster, ability.value);
            logic.DamagePlayer_Event(caster, target, damage);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            int damage = GetDamage(logic.GameData, caster, ability.value);
            logic.DamageCard_Event(caster, target, damage, true);

            //DoEffect(logic, ability, caster, target.slot);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            /*
            int damage = GetDamage(logic.GameData, caster, ability.value);
            logic.DamageCard(caster, logic.GameData.GetSlotCard(target), damage, true);
            */

            int damage = GetDamage(logic.GameData, caster, ability.value);
            logic.DamageCard_Event(caster, target, damage, true);
        }

        private int GetDamage(Game data, Card caster, int value)
        {
            if (value_type == EffectValueType.Attack)
                value = caster.GetAttack();
            if (value_type == EffectValueType.Health)
                value = caster.GetHP();
                
            Player player = data.GetPlayer(caster.player_id);
            int damage = value + caster.GetTraitValue(bonus_damage) + player.GetTraitValue(bonus_damage);
            return damage;
        }

    }
}