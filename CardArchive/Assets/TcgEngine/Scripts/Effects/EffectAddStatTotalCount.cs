using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that adds or removes basic card/player stats such as hp, attack, mana
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/AddStatTotalCount", order = 10)]
    public class EffectAddStatTotalCount : EffectData
    {
        public EffectStatType stat_type;
        public EffectTotalCountType total_type;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            int val = GetValue(logic, ability, caster);

            if (stat_type == EffectStatType.HP)
            {
                target.hp += val;
                target.hp_max += val;
                /*
                target.hp += ability.value;
                target.hp_max += ability.value;
                */
            }

            if (stat_type == EffectStatType.Mana)
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

            if (stat_type == EffectStatType.Attack)
                target.attack += val;
            if (stat_type == EffectStatType.HP)
                target.hp += val;
            if (stat_type == EffectStatType.Mana)
                target.mana += val;
            if (stat_type == EffectStatType.Range)
                target.range += val;
        }

        public override void DoOngoingEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            int val = GetValue(logic, ability, caster);

            if (stat_type == EffectStatType.Attack)
                target.attack_ongoing += val;
            if (stat_type == EffectStatType.HP)
                target.hp_ongoing += val;
            if (stat_type == EffectStatType.Mana)
                target.mana_ongoing += val;
            if (stat_type == EffectStatType.Range)
                target.range_ongoing += val;
        }

        public int GetValue(GameLogic logic, AbilityData ability, Card caster)
        {
            int val = 0;
            Player p = logic.GetGameData().GetPlayer(caster.player_id);

            if (total_type == EffectTotalCountType.TotalHeal)
                val = p.total_heal;

            return val;
        }

    }
    
    public enum EffectTotalCountType
    {
        None = 0,
        TotalHeal = 10,
    }
}