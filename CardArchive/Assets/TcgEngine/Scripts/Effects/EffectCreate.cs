using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effects that creates a new card from a CardData
    /// Use for discover effects
    /// Unlike EffectSummon, this effect targets the card data you want to create, and goes into the pile selected here
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/Create", order = 10)]
    public class EffectCreate : EffectData
    {
        public PileType create_pile;   //Better to not select Board here, for placing a card on board or in secret area, would suggest instead EffectSummon, or EffectPlay as chain after Create
        public bool create_opponent;       //Add to opponent?

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, CardData target)
        {
            Player player = logic.GameData.GetPlayer(caster.player_id);
            if (create_opponent)
                player = logic.GameData.GetOpponentPlayer(caster.player_id);

            logic.CreateCardToPile(target, caster.VariantData, player, create_pile);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            DoEffect(logic, ability, caster, target.CardData); //Create a copy
        }
    }
}