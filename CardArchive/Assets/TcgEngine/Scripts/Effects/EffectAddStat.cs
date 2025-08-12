using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that adds or removes basic card/player stats such as hp, attack, mana
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/AddStat", order = 10)]
    public class EffectAddStat : EffectData
    {
        public EffectStatType type;

        [Header("Use Stored Value")]
        public bool use_stored_value;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            int val = GetValue(logic, ability, caster);

            if (type == EffectStatType.HP)
            {
                target.hp += val;
                target.hp_max += val;
                /*
                target.hp += ability.value;
                target.hp_max += ability.value;
                */
            }

            if (type == EffectStatType.Mana)
            {
                /*
                target.mana += ability.value;
                target.mana_max += ability.value;
                target.mana = Mathf.Max(target.mana, 0);
                target.mana_max = Mathf.Clamp(target.mana_max, 0, GameplayData.Get().mana_max);
                */

                target.mana += val;
                target.mana_max += val;
                target.mana = Mathf.Max(target.mana, 0);
                target.mana_max = Mathf.Clamp(target.mana_max, 0, GameplayData.Get().mana_max);
            }
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            int val = GetValue(logic, ability, caster);

            if (type == EffectStatType.Attack)
                target.attack += val;
            if (type == EffectStatType.HP)
                target.hp += val;
            if (type == EffectStatType.Mana)
                target.mana += val;
            if (type == EffectStatType.Range)
                target.range += val;
        }

        public override void DoOngoingEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            int val = GetValue(logic, ability, caster);

            if (type == EffectStatType.Attack)
                target.attack_ongoing += val;
            if (type == EffectStatType.HP)
                target.hp_ongoing += val;
            if (type == EffectStatType.Mana)
            {
                target.mana_ongoing += val;
            }
            if (type == EffectStatType.Range)
                target.range_ongoing += val;
        }

        public int GetValue(GameLogic logic, AbilityData ability, Card caster)
        {
            int val = ability.value;

            if (use_stored_value && caster.HasStatus(StatusType.StoreValue))
                val += caster.GetStatusValue(StatusType.StoreValue);
    
            return val;
        }

    }

    public enum EffectStatType
    {
        None = 0,
        Attack = 10,
        HP = 20,
        Mana = 30,
        Range = 40,
    }
}