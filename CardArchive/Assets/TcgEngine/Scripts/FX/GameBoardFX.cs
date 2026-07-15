using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Client;
using TcgEngine.UI;

namespace TcgEngine.FX
{
    /// <summary>
    /// FX that are not related to any card/player, and appear in the middle of the board
    /// Usually when big abilities are played
    /// </summary>

    public class GameBoardFX : MonoBehaviour
    {
        void Start()
        {
            GameClient client = GameClient.Get();
            client.onNewTurn += OnNewTurn;
            client.onAttackPhase += OnAttackPhase;
            client.onCardPlayed += OnPlayCard;
            client.onCardDraw += OnCardDraw;
            client.onCardDissolved += OnDissolveCard;
            client.onExhaustDamage += OnExhaustDamage;
            client.onAbilityStart += OnAbility;
            client.onSecretTrigger += OnSecret;
            client.onValueRolled += OnRoll;
        }

        void OnNewTurn(int player_id)
        {
            AudioTool.Get().PlaySFX("turn", AssetData.Get().new_turn_audio);

            GameObject prefab = FXTool.DoFX(AssetData.Get().new_turn_fx, Vector3.zero, 1.0f);
            TurnStartUI ui = prefab.GetComponentInChildren<TurnStartUI>();
            ui.SetTurn(player_id);
        }

        void OnAttackPhase(int player_id)
        {
            GameObject prefab = FXTool.DoFX(AssetData.Get().new_turn_fx, Vector3.zero, 1.0f);
            TurnStartUI ui = prefab.GetComponentInChildren<TurnStartUI>();
            ui.SetTurn(player_id);
        }

        void OnPlayCard(Card card, Slot slot)
        {
            int player_id = GameClient.Get().GetPlayerID();
            if (card != null)
            {
                CardData icard = CardData.Get(card.card_id);
                if (icard.type == CardType.Spell)
                {
                    GameObject prefab = player_id == card.player_id ? AssetData.Get().play_card_fx : AssetData.Get().play_card_other_fx;
                    GameObject obj = FXTool.DoUniqueFX("card_zoom", prefab, Vector3.zero);
                    CardUI ui = obj.GetComponentInChildren<CardUI>();
                    ui.SetCard(icard, card.VariantData);

                    AudioClip spawn_audio = icard.spawn_audio != null ? icard.spawn_audio : AssetData.Get().card_spawn_audio;
                    AudioTool.Get().PlaySFX("card_spell", spawn_audio);
                }

                if (icard.type == CardType.Secret)
                {
                    GameObject sprefab = player_id == card.player_id ? AssetData.Get().play_secret_fx : AssetData.Get().play_secret_other_fx;
                    FXTool.DoFX(sprefab, Vector3.zero);

                    AudioClip spawn_audio = icard.spawn_audio != null ? icard.spawn_audio : AssetData.Get().card_spawn_audio;
                    AudioTool.Get().PlaySFX("card_spell", spawn_audio);
                }
            }
        }

        void OnCardDraw(int nb)
        {
            
            AudioClip draw_audio = AssetData.Get().card_draw_audio;
            AudioTool.Get().PlaySFX("card_draw", draw_audio);
            
        }

        void OnExhaustDamage(int player_id)
        {
            bool opponent = player_id != GameClient.Get().GetPlayerID();
            BoardSlotPlayer bslot = BoardSlotPlayer.Get(opponent);
            if (bslot != null)
            {
                FXTool.DoFX(AssetData.Get().player_exhausted_fx, bslot.transform.position);
                AudioTool.Get().PlaySFX("player_damage", AssetData.Get().player_damage_audio);
            }
        }

        void OnDissolveCard(Card card, int owner_player_id)
        {
            if (card == null)
                return;

            //스크린 UI 연출이므로 게임 캔버스 아래에 스폰 (루트가 stretch라 화면 전체에 맞춰짐)
            GameUI game_ui = GameUI.Get();
            if (game_ui == null || game_ui.game_canvas == null)
                return;

            GameObject obj = FXTool.DoUIFX(AssetData.Get().card_dissolve_fx, game_ui.game_canvas.transform, FXLayer.OverHand, 2.0f);
            if (obj == null)
                return;

            //재생 전에 카드 정보 주입, 연출은 프리팹의 Self/Enemy 애니메이션이 담당
            CardUI ui = obj.GetComponentInChildren<CardUI>(true);
            if (ui != null)
                ui.SetCard(CardData.Get(card.card_id), card.VariantData);

            FXSetting setting = obj.GetComponentInChildren<FXSetting>(true);
            if (setting != null)
                setting.PlayOwnerState(owner_player_id);
        }

        private void OnAbility(AbilityData iability, Card caster)
        {
            if (iability != null)
            {
                FXTool.DoFX(iability.board_fx, Vector3.zero);

                if (iability.show_card_fx && caster != null)
                {
                    CardData icard = CardData.Get(caster.card_id);
                    if (icard != null && icard.type != CardType.Spell) //Spells already show the zoom in OnPlayCard
                    {
                        int player_id = GameClient.Get().GetPlayerID();
                        GameObject prefab = player_id == caster.player_id ? AssetData.Get().play_card_fx : AssetData.Get().play_card_other_fx;
                        GameObject obj = FXTool.DoUniqueFX("card_zoom", prefab, Vector3.zero);
                        CardUI ui = obj.GetComponentInChildren<CardUI>();
                        ui.SetCard(icard, caster.VariantData);
                    }
                }
            }
        }

        private void OnSecret(Card secret, Card triggerer)
        {
            CardData icard = CardData.Get(secret.card_id);
            if (icard?.attack_audio != null)
                AudioTool.Get().PlaySFX("card_secret", icard.attack_audio);
        }

        private void OnRoll(int value)
        {
            GameObject fx = FXTool.DoFX(AssetData.Get().dice_roll_fx, Vector3.zero);
            DiceRollFX dice = fx?.GetComponent<DiceRollFX>();
            if (dice != null)
            {
                dice.value = value;
            }
        }

    }
}