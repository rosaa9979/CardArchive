using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effects that heals a card or player (hp)
    /// It cannot restore more than the original hp, use AddStats to go beyond original
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/Heal", order = 10)]
    public class EffectHeal : EffectData
    {
        public EffectValueType heal_type;
        public TraitData bonus_heal;
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            int heal = GetHeal(logic.GameData, caster, ability.value);
            logic.HealPlayer(target, heal);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            int heal = GetHeal(logic.GameData, caster, ability.value);
            logic.HealCard(target, heal);
        }

        private int GetHeal(Game data, Card caster, int value)
        {
            if (heal_type == EffectValueType.Attack)
                value = caster.GetAttack();
            if (heal_type == EffectValueType.Health)
                value = caster.GetHP();
                
            Player player = data.GetPlayer(caster.player_id);
            int heal = value + caster.GetTraitValue(bonus_heal) + player.GetTraitValue(bonus_heal);
            return heal;
        }
    }
}