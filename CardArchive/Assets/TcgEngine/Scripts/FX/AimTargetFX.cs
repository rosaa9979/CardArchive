using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Client;

namespace TcgEngine.FX
{
    /// <summary>
    /// The crosshair target that appears when targeting with a spell
    /// </summary>

    public class AimTargetFX : MonoBehaviour
    {
        public GameObject fx;

        void Start()
        {

        }

        void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            Game game_data = GameClient.Get().GetGameData();
            Vector3 dest = GameBoard.Get().RaycastMouseBoard();
            BSlot bslot = BSlot.GetNearest(dest);

            bool visible = false;

            if (game_data.selector == SelectorType.SelectTarget && bslot != null)
            {
                AbilityData ability = AbilityData.Get(game_data.selector_ability_id);
                Card caster = game_data.GetCard(game_data.selector_caster_uid);
                Card target = game_data.GetSlotCard(bslot.GetSlot());

                if (ability.CanTarget(game_data, caster, bslot.GetSlot()))
                {
                    visible = true;
                }

                if (ability.CanTarget(game_data, caster, target))
                {
                    visible = true;
                }
            }
            
            /*
            HandCard hcard = HandCard.GetDrag();
            if (hcard != null)
            {
                Card caster = hcard.GetCard();
                if (caster.CardData.IsRequireTarget())
                    visible = true;
            }
            */

            if (fx.activeSelf != visible)
                fx.SetActive(visible);

            if (visible)
            {
                transform.position = dest;
            }
        }
    }
}
