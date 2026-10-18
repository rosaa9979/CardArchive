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
        public MarkerData bonus_heal;
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            int heal = GetHeal(logic.GameData, caster, ability.value);
            logic.HealPlayer(target, heal, caster);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            int heal = GetHeal(logic.GameData, caster, ability.value);
            logic.HealCard(target, heal, caster);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            int heal = GetHeal(logic.GameData, caster, ability.value);
            logic.ApplyTileEffect(target,
                card => logic.HealCard(card, heal, caster),
                player => logic.HealPlayer(player, heal, caster));
        }

        private int GetHeal(Game data, Card caster, int value)
        {
            if (heal_type == EffectValueType.Attack)
                value = caster.GetAttack();
            if (heal_type == EffectValueType.Health)
                value = caster.GetHP();
                
            Player player = data.GetPlayer(caster.player_id);
            int heal = value + caster.GetMarkerValue(bonus_heal) + player.GetMarkerValue(bonus_heal);
            return heal;
        }
    }
}