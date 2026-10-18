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



    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/Damage", order = 10)]
    public class EffectDamage : EffectData
    {
        public EffectValueType value_type;
        public TraitData bonus_damage;

        public const string StoredSlotTrait = "stored_slot";

        // No-target damage decodes the stored position and uses the same tile effect route.
        // If no position is stored, leave the request unchanged.
        //Used by effects on a card that previously stored a slot via EffectSetStatCustom (e.g. "포격").
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster)
        {
            int code = caster.GetTraitValue(StoredSlotTrait);
            if (code <= 0)
                return; //no slot stored -> pass

            Slot slot = Slot.FromTraitCode(code);
            DoEffect(logic, ability, caster, slot);

            //Consume (reset stored_slot to 0) is handled by a separate chained ability/effect, not here.
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            int damage = GetDamage(logic.GameData, caster, ability.value);
            logic.DamagePlayer_Event(caster, target, damage);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            int damage = GetDamage(logic.GameData, caster, ability.value);
            logic.DamageCard_Event(caster, target, damage, true);

        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {

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