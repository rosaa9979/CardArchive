using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Client;
using TcgEngine.UI;
using TMPro;

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
            client.onPlayerDamaged += OnPlayerDamaged;
            client.onCardDamaged += OnCardDamaged;
            client.onAbilityStart += OnAbility;
            client.onSecretTrigger += OnSecret;
            client.onValueRolled += OnRoll;
        }

        void OnNewTurn(int player_id)
        {
            AudioTool.Get().PlaySFX("turn", AssetData.Get().new_turn_audio);

            GameObject prefab = FXTool.DoFX(AssetData.Get().new_turn_fx, Vector3.zero, ownerPlayerId: player_id);
            TurnStartUI ui = prefab.GetComponentInChildren<TurnStartUI>();
            ui.SetTurn(player_id);
        }

        void OnAttackPhase(int player_id)
        {
            GameObject prefab = FXTool.DoFX(AssetData.Get().new_turn_fx, Vector3.zero, ownerPlayerId: player_id);
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
                    // The acting player already saw the card in hand. Automatic plays still reveal it.
                    if (player_id != card.player_id || !GameClient.Get().IsDirectCardPlay(card))
                        ShowCardPreview(card);

                    AudioClip spawn_audio = icard.spawn_audio != null ? icard.spawn_audio : AssetData.Get().card_spawn_audio;
                    AudioTool.Get().PlaySFX("card_spell", spawn_audio);
                }

                if (icard.type == CardType.Secret)
                {
                    GameObject sprefab = player_id == card.player_id ? AssetData.Get().play_secret_fx : AssetData.Get().play_secret_other_fx;
                    FXTool.DoFX(sprefab, Vector3.zero, ownerPlayerId: card.player_id);

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

        void OnExhaustDamage(int player_id, int damage)
        {
            bool opponent = player_id != GameClient.Get().GetPlayerID();

            //Self/Enemy 프리팹 모두 원점 기준으로 디자인되어 있으므로 (0,0)에 스폰
            GameObject prefab = opponent ? AssetData.Get().player_exhausted_other_fx : AssetData.Get().player_exhausted_fx;
            GameObject fx = FXTool.DoFX(prefab, Vector3.zero, FXLayer.OverHand, ownerPlayerId: player_id);

            TMP_Text text = fx != null ? fx.GetComponentInChildren<TMP_Text>(true) : null;
            if (text != null)
                text.text = string.Format("기적의 대가로,\n{0}의 피해를 입습니다", damage);

            AudioTool.Get().PlaySFX("player_damage", AssetData.Get().player_damage_audio);
        }

        void OnCardDamaged(Card attacker, Card target, int value)
        {
            if (target == null) return;
            BoardCard board = BoardCard.Get(target.uid);
            BSlot slot = BSlot.Get(target.slot);
            Vector3 position = board != null ? board.transform.position : slot != null ? slot.GetPosition(target.slot) : Vector3.zero;
            GameObject fx = FXTool.DoFX(AssetData.Get().damage_fx, position, FXLayer.OverHand, ownerPlayerId: target.player_id);
            if (fx != null) fx.GetComponent<DamageFX>()?.SetValue(value);
        }

        void OnPlayerDamaged(Card attacker, Player target, int value)
        {
            if (target == null)
                return;

            //Real applied damage from the server — anchored on the damaged player's zone so it
            //works for any source (attack, ability), not only when the attacker is on board
            bool opponent = target.player_id != GameClient.Get().GetPlayerID();
            BoardSlotPlayer zone = BoardSlotPlayer.Get(opponent);
            if (zone == null)
                return;

            // This callback is dispatched by the same Impact that publishes HP.
            GameObject fx = FXTool.DoFX(AssetData.Get().damage_fx, zone.transform.position, FXLayer.OverHand, ownerPlayerId: target.player_id);
            if (fx != null) fx.GetComponent<DamageFX>()?.SetValue(value);
        }

        void OnDissolveCard(Card card, int owner_player_id)
        {
            if (card == null)
                return;

            //스크린 UI 연출이므로 게임 캔버스 아래에 스폰 (루트가 stretch라 화면 전체에 맞춰짐)
            GameUI game_ui = GameUI.Get();
            if (game_ui == null || game_ui.game_canvas == null)
                return;

            GameObject obj = FXTool.DoUIFX(AssetData.Get().card_dissolve_fx, game_ui.game_canvas.transform, FXLayer.OverHand, ownerPlayerId: owner_player_id);
            if (obj == null)
                return;

            //재생 전에 카드 정보 주입, 연출은 프리팹의 Self/Enemy 애니메이션이 담당
            CardUI ui = obj.GetComponentInChildren<CardUI>(true);
            if (ui != null)
                ui.SetCard(card);

        }

        private void OnAbility(AbilityData iability, Card caster)
        {
            if (iability != null)
            {
                FXTool.ScheduleStateFX(iability.board_fx, Vector3.zero, priority: 1, ownerPlayerId: caster?.player_id);

                if (iability.show_card_fx && caster != null)
                {
                    CardData icard = CardData.Get(caster.card_id);
                    if (icard != null && icard.type != CardType.Spell) //Spells already show the zoom in OnPlayCard
                    {
                        // Suppress only the local card's entry abilities, not its later triggers or secrets.
                        bool ownDirectEntry = caster.player_id == GameClient.Get().GetPlayerID()
                            && icard.type != CardType.Secret && GameClient.Get().IsDirectCardPlay(caster)
                            && (iability.trigger == AbilityTrigger.OnPlay || iability.trigger == AbilityTrigger.OnUse);
                        if (!ownDirectEntry)
                            ShowCardPreview(caster);
                    }
                }
            }
        }

        private void ShowCardPreview(Card card)
        {
            GameObject prefab = GameClient.Get().GetPlayerID() == card.player_id
                ? AssetData.Get().play_card_fx : AssetData.Get().play_card_other_fx;
            GameObject obj = FXTool.DoUniqueFX("card_zoom", prefab, Vector3.zero, ownerPlayerId: card.player_id);
            CardUI ui = obj != null ? obj.GetComponentInChildren<CardUI>() : null;
            if (ui != null)
                ui.SetCard(card);
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
