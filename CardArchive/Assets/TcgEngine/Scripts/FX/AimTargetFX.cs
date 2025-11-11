using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Client;
using TMPro;

namespace TcgEngine.FX
{
    /// <summary>
    /// The crosshair target that appears when targeting with a spell
    /// </summary>

    public class AimTargetFX : MonoBehaviour
    {
        public GameObject target_fx;
        public GameObject text_fx;

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
            bool text_visible = false;

            if (game_data.selector == SelectorType.SelectTarget && game_data.IsPlayerSelectorTurn(GameClient.Get().GetPlayer()))
            {
                AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

                if (!string.IsNullOrWhiteSpace(ability.selector_desc))
                {
                    text_visible = true;
                    TextMeshPro tmpro_text = text_fx.GetComponentInChildren<TextMeshPro>();
                    tmpro_text.text = ability.selector_desc;
                }

                if (bslot != null)
                {
                    Card caster = game_data.GetCard(game_data.selector_caster_uid);
                    Card target = game_data.GetSlotCard(bslot.GetSlot());
                    Player player = bslot.GetPlayer();

                    if (ability.criteria_target == AbilityTarget.SelectTarget && ability.CanTarget(game_data, caster, bslot.GetSlot()))
                    {
                        visible = true;
                    }
                }
            }

            HandCard hcard = HandCard.GetDrag();
            if (hcard != null)
            {
                Card caster = hcard.GetCard();

                if (caster.CardData.IsRequireTarget())
                {
                    AbilityData ability = caster.GetAbility(AbilityTarget.PlayTarget);

                    if (!string.IsNullOrWhiteSpace(ability.selector_desc))
                    {
                        text_visible = true;
                        TextMeshPro tmpro_text = text_fx.GetComponentInChildren<TextMeshPro>();
                        tmpro_text.text = ability.selector_desc;
                    }

                    if (bslot != null)
                    {
                        Card target = game_data.GetSlotCard(bslot.GetSlot());
                        Player player = bslot.GetPlayer();

                        if (ability.CanTarget(game_data, caster, bslot.GetSlot()))
                        {
                            visible = true;
                        }
                        if (ability.CanTarget(game_data, caster, target))
                            visible = true;
                        if (ability.CanTarget(game_data, caster, player))
                            visible = true;
                    }
                }
            }
            

            if (target_fx.activeSelf != visible)
                target_fx.SetActive(visible);

            if (text_fx.activeSelf != text_visible)
                text_fx.SetActive(text_visible);
            
            if (visible || text_visible)
                transform.position = dest;
        }
    }
}
