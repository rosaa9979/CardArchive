using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    //Effect to Summon an entirely new card (not in anyones deck)
    //And places it on the board (if target slot) or hand (if target player)
    //Unlike EffectCreate, this effect targets where the card goes, and the carddata is selected on the effect

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/UseCard", order = 10)]
    public class EffectUseCard : EffectData
    {
        public CardData use;
        public bool use_opponent;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster)
        {
            Player player = logic.GameData.GetPlayer(caster.player_id);
            Player oplayer = logic.GameData.GetOpponentPlayer(player.player_id);

            if (use_opponent)
                logic.UseCard(oplayer, use, caster.VariantData, Slot.None); 
            else
                logic.UseCard(player, use, caster.VariantData, Slot.None); 
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            Player otarget = logic.GameData.GetOpponentPlayer(target.player_id);
            if (use_opponent)
                logic.SummonCardHand(otarget, use, caster.VariantData); //Summon in hand instead of board when target a player
            else
                logic.SummonCardHand(target, use, caster.VariantData);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            Player player = logic.GameData.GetPlayer(caster.player_id);
            Player oplayer = logic.GameData.GetOpponentPlayer(player.player_id);

            if (use_opponent)
                logic.UseCard(oplayer, use, caster.VariantData, target.slot); //Assumes the target has just been killed, so the slot is empty
            else
                logic.UseCard(player, use, caster.VariantData, target.slot);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            Player player = logic.GameData.GetPlayer(caster.player_id);
            Player oplayer = logic.GameData.GetOpponentPlayer(player.player_id);

            if (use_opponent)
                logic.UseCard(oplayer, use, caster.VariantData, target);
            else
                logic.UseCard(player, use, caster.VariantData, target);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, CardData target)
        {
            Player player = logic.GameData.GetPlayer(caster.player_id);
            Player oplayer = logic.GameData.GetOpponentPlayer(player.player_id);

            if (use_opponent)
                logic.SummonCardHand(oplayer, target, caster.VariantData);   //Summon in hand instead of board when target a carddata
            else
                logic.SummonCardHand(player, target, caster.VariantData);
        }
    }
}