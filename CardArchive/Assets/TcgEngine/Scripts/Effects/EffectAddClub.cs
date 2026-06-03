using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that adds card/player custom stats or traits
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/AddClub", order = 10)]
    public class EffectAddClub : EffectData
    {
        public ClubData club;

        /*
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            target.AddTrait(trait.id, ability.value);
        }
        */

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            logic.AddClub(caster, target.card_id); //GameLogic fires OnAddClubOther
        }

        /*
        public override void DoOngoingEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            target.AddOngoingTrait(trait.id, ability.value);
        }

        
        public override void DoOngoingEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            target.AddOngoingTrait(trait.id, ability.value);
        }
        */
    }
}