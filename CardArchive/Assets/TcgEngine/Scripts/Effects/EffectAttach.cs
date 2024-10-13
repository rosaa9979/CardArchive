using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    //Effect to Summon an entirely new card (not in anyones deck)
    //And places it on the board (if target slot) or hand (if target player)
    //Unlike EffectCreate, this effect targets where the card goes, and the carddata is selected on the effect

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/AttachCard", order = 10)]
    public class EffectAttachCard : EffectData
    {
        public CardData attach;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            Player player = logic.GetGameData().GetPlayer(caster.player_id);
            Card card = Card.Create(attach, caster.VariantData, player);

            logic.AttachCard(target, card);
        }
    }
}