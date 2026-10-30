using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using UnityEngine.Events;
using TcgEngine;
using DG.Tweening;
using System.Xml;

namespace TcgEngine.FX
{
    /// <summary>
    /// All FX/anims related to a card on the board
    /// </summary>

    public class BoardCardFX : MonoBehaviour
    {
        public Material kill_mat;
        public string kill_mat_fade = "noise_fade";

        [Header("Stat")]
        public RectTransform attack;
        public RectTransform health;
        public RectTransform range;


        private BoardCard bcard;

        private ParticleSystem exhausted_fx = null;
        private OngoingAbilityFX ongoing_fx;

        private Dictionary<StatusType, GameObject> status_fx_list = new Dictionary<StatusType, GameObject>();

        void Awake()
        {
            bcard = GetComponent<BoardCard>();
            ongoing_fx = new OngoingAbilityFX(transform);
            bcard.onKill += OnKill;
        }

        void Start()
        {
            GameClient client = GameClient.Get();
            client.onCardMoved += OnMove;
            client.onAttackStart += OnAttack;
            client.onAttackHit += OnAttackHit;
            client.onAttackEvade += OnAttackEvade;
            client.onAttackPlayerStart += OnAttackPlayer;
            client.onAttackPlayerHit += OnAttackPlayerHit;
            client.onAbilityStart += OnAbilityStart;
            client.onAbilityTargetCard += OnAbilityEffect;
            client.onAbilityEnd += OnAbilityAfter;
            client.onCardStatChange += OnStatChange;

            OnSpawn();
        }

        private void OnDestroy()
        {
            ongoing_fx?.Clear();
            GameClient client = GameClient.Get();
            client.onCardMoved -= OnMove;
            client.onAttackStart -= OnAttack;
            client.onAttackHit -= OnAttackHit;
            client.onAttackEvade -= OnAttackEvade;
            client.onAttackPlayerStart -= OnAttackPlayer;
            client.onAttackPlayerHit -= OnAttackPlayerHit;
            client.onAbilityStart -= OnAbilityStart;
            client.onAbilityTargetCard -= OnAbilityEffect;
            client.onAbilityEnd -= OnAbilityAfter;
            client.onCardStatChange -= OnStatChange;
        }

        void Update()
        {
            GameClient client = GameClient.Get();
            if (client == null || !client.IsReady())
            {
                ongoing_fx?.Clear();
                return;
            }

            Card card = bcard.GetCard(); // Published presentation state, including replay.
            if (client.CanShowOngoingFX(card)) ongoing_fx.Sync(client.GetGameData(), card, bcard.IsDead());
            else ongoing_fx.Clear();
            if (card == null) return;

            //Status FX
            List<CardStatus> status_all = card.GetAllStatus();
            foreach (CardStatus status in status_all)
            {
                StatusData istatus = StatusData.Get(status.type);
                if (istatus != null && !status_fx_list.ContainsKey(status.type) && istatus.status_fx != null)
                {
                    GameObject fx = FXTool.DoPersistentFX(istatus.status_fx, transform, card.player_id);
                    status_fx_list[istatus.effect] = fx;
                }
            }

            foreach (GameObject fx in status_fx_list.Values)
                FXTool.SetOwner(fx, card.player_id);

            //Remove status FX
            List<StatusType> remove_list = new List<StatusType>();
            foreach (KeyValuePair<StatusType, GameObject> pair in status_fx_list)
            {
                if (!card.HasStatus(pair.Key))
                {
                    remove_list.Add(pair.Key);
                    Destroy(pair.Value);
                }
            }

            foreach (StatusType status in remove_list)
                status_fx_list.Remove(status);

            //Exhausted add/remove
            if (exhausted_fx != null && !exhausted_fx.isPlaying && card.exhausted)
                exhausted_fx.Play();
            if (exhausted_fx != null && exhausted_fx.isPlaying && !card.exhausted)
                exhausted_fx.Stop();
        }

        private void OnSpawn()
        {
            CardData icard = bcard.GetCardData();

            //Spawn Audio
            AudioClip audio = icard?.spawn_audio != null ? icard.spawn_audio : AssetData.Get().card_spawn_audio;
            AudioTool.Get().PlaySFX("card_spawn", audio);

            //Spawn FX
            GameObject spawn_fx = icard.spawn_fx != null ? icard.spawn_fx : AssetData.Get().card_spawn_fx;
            if (!GameClient.Get().ConsumeSpawnPresentation(bcard.GetCardUID()))
                GameClient.Get().ScheduleBoardSpawnFX(bcard.GetCard(), spawn_fx, transform.position);

            //Spawn dissolve fx
            if (GameTool.IsURP())
            {
                SpriteRenderer render = bcard.card_sprite;
                render.material = kill_mat;

                FadeSetVal(bcard.card_sprite, 0f);
                FadeKill(bcard.card_sprite, 1f, 0.5f);
            }

            //Exhausted fx
            if (AssetData.Get().card_exhausted_fx != null)
            {
                GameObject efx = FXTool.DoPersistentFX(AssetData.Get().card_exhausted_fx, transform, bcard.GetCard().player_id);
                efx.transform.localPosition = Vector3.zero;
                exhausted_fx = efx.GetComponent<ParticleSystem>();
            }

            //Idle status
            TimeTool.WaitFor(1f, () =>
            {
                if (this == null || bcard == null || bcard.IsDead()) return;
                Card displayed = bcard.GetCard();
                if (displayed != null && displayed.CardData == icard && icard.idle_fx != null)
                    FXTool.DoPersistentFX(icard.idle_fx, transform, displayed.player_id);
            });
        }

        private void OnKill()
        {
            ongoing_fx?.Clear();
            StartCoroutine(KillRoutine());
        }

        private IEnumerator KillRoutine()
        {
            bool alreadyPresented = GameClient.Get().ConsumeDeathPresentation(bcard.GetCardUID());
            CardData icard = bcard.GetCardData();

            //Death FX
            GameObject death_fx = icard.death_fx != null ? icard.death_fx : AssetData.Get().card_destroy_fx;
            if (!alreadyPresented)
            {
                FXTool.ScheduleStateFX(death_fx, transform.position, 0.5f, priority: 0, ownerPlayerId: bcard.GetCard().player_id);
                yield return new WaitForSeconds(0.5f);
            }

            //Death audio
            AudioClip audio = icard?.death_audio != null ? icard.death_audio : AssetData.Get().card_destroy_audio;
            AudioTool.Get().PlaySFX("card_spawn", audio);

            //Death dissolve fx
            if (GameTool.IsURP())
            {
                FadeKill(bcard.card_sprite, 0f, 0.5f);

                // Presented deaths dissolve without the 0.5s lead-in, so the UI canvases
                // (hidden at 0.8s by BoardCard.Kill) would otherwise outlive the art.
                if (alreadyPresented)
                    bcard.FadeOutCanvases(0.5f);
            }
        }
		
		private void FadeSetVal(SpriteRenderer render, float val)
        {
            render.material = kill_mat;
            render.material.SetFloat(kill_mat_fade, val);
        }

        private void FadeKill(SpriteRenderer render, float val, float duration)
        {
            AnimMatFX anim = AnimMatFX.Create(render.gameObject, render.material);
            anim.SetFloat(kill_mat_fade, val, duration);
        }

        private void OnMove(Card card, Slot slot)
        {
            AudioTool.Get().PlaySFX("card_move", AssetData.Get().card_move_audio);
        }

        private void OnAttack(Card attacker, Card target)
        {
            /*
            Card card = bcard.GetCard();
            CardData icard = bcard.GetCardData();
            if (attacker == null || target == null)
                return;

            if (card.uid == attacker.uid)
            {
                BoardCard battacker = BoardCard.Get(attacker.uid);
                BoardCard btarget = BoardCard.Get(target.uid);
                if (btarget != null)
                {
                    //Card charge into target
                    //ChargeInto(btarget);

                    //Show Damage Number FX on self
                    //if(!attacker.HasStatus(StatusType.Intimidate))
                    //    DamageFX(target, attacker, transform);

                    //Attack FX and Audio
                    GameObject fx = (icard.weapon != null && icard.weapon.attack_fx != null) ? icard.weapon.attack_fx
                                  : icard.attack_fx != null ? icard.attack_fx
                                  : AssetData.Get().card_attack_fx;
                    GameObject fx_result = FXTool.DoFX(fx, battacker.transform.position);
                    fx_result.transform.rotation = FXTool.GetFXRotation(battacker.gameObject, btarget.gameObject);
                    AudioClip audio = (icard?.weapon != null && icard.weapon.attack_audio != null) ? icard.weapon.attack_audio
                                    : icard?.attack_audio != null ? icard.attack_audio
                                    : AssetData.Get().card_attack_audio;
                    AudioTool.Get().PlaySFX("card_attack", audio);
                }
            }

            if (card.uid == target.uid)
            {
                if (target.CardData.IsCitizen() || card == target)
                {
                    //Show Damage Number FX on self
                    DamageFX(attacker, target, transform);
                }
            }
            */
        }

        private void OnAttackHit(Card attacker, Card target)
        {
            Card card = bcard.GetCard();
            CardData icard = bcard.GetCardData();

            if (attacker == null || target == null)
                return;

            if (card.uid == attacker.uid)
            {
                BoardCard battacker = BoardCard.Get(attacker.uid);
                BoardCard btarget = BoardCard.Get(target.uid);
                if (btarget != null)
                {
                    //Card charge into target
                    //ChargeInto(btarget);

                    //Show Damage Number FX on self
                    //if(!attacker.HasStatus(StatusType.Intimidate))
                    //    DamageFX(target, attacker, transform);

                    //Attack FX and Audio
                    GameObject fx = (icard.weapon != null && icard.weapon.attack_fx != null) ? icard.weapon.attack_fx
                                  : icard.attack_fx != null ? icard.attack_fx
                                  : AssetData.Get().card_attack_fx;
                    GameObject fx_result = FXTool.DoFX(fx, battacker.transform.position, ownerPlayerId: attacker.player_id);
                    fx_result.transform.rotation = FXTool.GetFXRotation(fx_result, battacker.gameObject, btarget.gameObject);
                    //GameObject fx_result = FXTool.DoFX(fx, battacker.transform.position);
                    //fx_result.transform.rotation = FXTool.GetFXRotation(battacker.gameObject, btarget.gameObject);
                    AudioClip audio = (icard?.weapon != null && icard.weapon.attack_audio != null) ? icard.weapon.attack_audio
                                    : icard?.attack_audio != null ? icard.attack_audio
                                    : AssetData.Get().card_attack_audio;
                    AudioTool.Get().PlaySFX("card_attack", audio);
                }
            }

            if (card.uid == target.uid)
            {
                BoardCard battacker = BoardCard.Get(attacker.uid);
                BoardCard btarget = BoardCard.Get(target.uid);
                if (btarget != null)
                {
                    {
                        CardData iattacker = battacker.GetCardData();
                        GameObject fx = (iattacker?.weapon != null && iattacker.weapon.hit_fx != null) ? iattacker.weapon.hit_fx
                                      : AssetData.Get().card_attack_hit_fx;
                        FXTool.ScheduleStateFX(fx, transform.position, onCreated: fx_result =>
                            fx_result.transform.rotation = FXTool.GetFXRotation(fx_result, battacker != null ? battacker.gameObject : null, btarget != null ? btarget.gameObject : null), ownerPlayerId: attacker.player_id);
                        // Damage display waits for this animation's Impact marker.
                    }
                }
            }
        }

        private void OnAttackEvade(Card attacker, Card target)
        {
            Card card = bcard.GetCard();
            CardData icard = bcard.GetCardData();

            if (attacker == null || target == null)
                return;

            if (card.uid == attacker.uid)
            {
                BoardCard battacker = BoardCard.Get(attacker.uid);
                BoardCard btarget = BoardCard.Get(target.uid);
                if (btarget != null)
                {
                    //Card charge into target
                    //ChargeInto(btarget);

                    //Show Damage Number FX on self
                    //if(!attacker.HasStatus(StatusType.Intimidate))
                    //    DamageFX(target, attacker, transform);

                    //Attack FX and Audio
                    GameObject fx = (icard.weapon != null && icard.weapon.attack_fx != null) ? icard.weapon.attack_fx
                                  : icard.attack_fx != null ? icard.attack_fx
                                  : AssetData.Get().card_attack_fx;
                    GameObject fx_result = FXTool.DoFX(fx, battacker.transform.position, ownerPlayerId: attacker.player_id);
                    fx_result.transform.rotation = FXTool.GetFXRotation(fx_result, battacker.gameObject, btarget.gameObject);
                    //GameObject fx_result = FXTool.DoFX(fx, battacker.transform.position);
                    //fx_result.transform.rotation = FXTool.GetFXRotation(battacker.gameObject, btarget.gameObject);
                    AudioClip audio = (icard?.weapon != null && icard.weapon.attack_audio != null) ? icard.weapon.attack_audio
                                    : icard?.attack_audio != null ? icard.attack_audio
                                    : AssetData.Get().card_attack_audio;
                    AudioTool.Get().PlaySFX("card_attack", audio);
                }
            }

            if (card.uid == target.uid)
            {
                BoardCard battacker = BoardCard.Get(attacker.uid);
                BoardCard btarget = BoardCard.Get(target.uid);
                if (btarget != null)
                {
                    FXTool.ScheduleStateFX(AssetData.Get().floating_damage_fx, btarget.transform.position,
                        0.15f, 0, fx => fx.GetComponent<FloatingNumberFX>()?.SetValue("Miss!"), ownerPlayerId: target.player_id);
                }
            }
        }

        private void OnAttackPlayer(Card attacker, Player player)
        {
            /*
            if (attacker == null || player == null)
                return;

            Card card = bcard.GetCard();
            if (card.uid == attacker.uid)
            {
                bool is_other = player.player_id != GameClient.Get().GetPlayerID();
                CardData icard = bcard.GetCardData();
                BoardSlotPlayer zone = BoardSlotPlayer.Get(is_other);

                ChargeIntoPlayer(zone);

                AudioClip audio = (icard?.weapon != null && icard.weapon.attack_audio != null) ? icard.weapon.attack_audio
                                : icard?.attack_audio != null ? icard.attack_audio
                                : AssetData.Get().card_attack_audio;
                AudioTool.Get().PlaySFX("card_attack", audio);

                int value = bcard.GetCard().GetAttack();
                DamageFX(zone.transform, value);
            }
            */
        }
        
        private void OnAttackPlayerHit(Card attacker, Player player)
        {
            if (attacker == null || player == null)
                return;


            Card card = bcard.GetCard();
            if (card.uid == attacker.uid)
            {
                BoardCard battacker = BoardCard.Get(attacker.uid);
                bool is_other = player.player_id != GameClient.Get().GetPlayerID();
                CardData icard = bcard.GetCardData();
                BoardSlotPlayer zone = BoardSlotPlayer.Get(is_other);

                AudioClip audio = (icard?.weapon != null && icard.weapon.attack_audio != null) ? icard.weapon.attack_audio
                                : icard?.attack_audio != null ? icard.attack_audio
                                : AssetData.Get().card_attack_audio;
                AudioTool.Get().PlaySFX("card_attack", audio);

                //ChargeIntoPlayer(zone);
                GameObject attack_fx = (icard.weapon != null && icard.weapon.attack_fx != null) ? icard.weapon.attack_fx
                                     : icard.attack_fx != null ? icard.attack_fx
                                     : AssetData.Get().card_attack_fx;
                GameObject attack_fx_result = FXTool.DoFX(attack_fx, battacker.transform.position, ownerPlayerId: attacker.player_id);
                attack_fx_result.transform.rotation = FXTool.GetFXRotation(attack_fx_result, battacker.gameObject, zone.gameObject);

                {
                    GameObject hit_fx = (icard?.weapon != null && icard.weapon.hit_fx != null) ? icard.weapon.hit_fx
                                      : AssetData.Get().card_attack_hit_fx;
                    FXTool.ScheduleStateFX(hit_fx, zone.transform.position, onCreated: fx =>
                        fx.transform.rotation = FXTool.GetFXRotation(fx, battacker != null ? battacker.gameObject : null, zone != null ? zone.gameObject : null), ownerPlayerId: attacker.player_id);
                    // Damage display waits for this animation's Impact marker.
                }

                zone.player_ui.DOShakePosition(1f, 5f, 10, 90);
            }
        }

        private void ChargeInto(BoardCard target)
        {
            if (target != null)
            {
                ChargeInto(target.gameObject);

                CardData icard = target.GetCardData();
                GameObject prefab = icard.damage_fx ? icard.damage_fx : AssetData.Get().card_damage_fx;
                AudioClip audio = icard.damage_audio ? icard.damage_audio : AssetData.Get().card_damage_audio;
                FXTool.ScheduleStateFX(prefab, target.transform.position, 0.25f, onCreated: fx =>
                    AudioTool.Get().PlaySFX("card_hit", audio), ownerPlayerId: target.GetCard().player_id);
            }
        }

        private void ChargeIntoPlayer(BoardSlotPlayer target)
        {
            if (target != null)
            {
                ChargeInto(target.gameObject);

                FXTool.ScheduleStateFX(AssetData.Get().player_damage_fx, target.transform.position,
                    0.25f, onCreated: fx => AudioTool.Get().PlaySFX("card_hit", AssetData.Get().player_damage_audio), ownerPlayerId: target.GetPlayer()?.player_id);
            }
        }

        private void ChargeInto(GameObject target)
        {
            if (target != null)
            {
                int current_order = bcard.card_sprite.sortingOrder;
                Vector3 dir = target.transform.position - transform.position;
                Vector3 target_pos = target.transform.position - dir.normalized * 1f;
                Vector3 current_pos = transform.position;
                //bcard.SetOrder(current_order + 10);

                AnimFX anim = AnimFX.Create(gameObject);
                anim.MoveTo(current_pos - dir.normalized * 0.5f, 0.3f);
                anim.MoveTo(target.transform.position, 0.1f);
                anim.MoveTo(current_pos, 0.3f);
                /*
                anim.Callback(0f, () =>
                {
                    if (bcard != null)
                        bcard.SetOrder(current_order);
                });
                */
            }
        }

        private void OnAbilityStart(AbilityData iability, Card caster)
        {
            if (iability != null && caster != null)
            {
                if (caster.uid == bcard.GetCardUID())
                {
                    FXTool.ScheduleStateFX(iability.caster_fx, bcard.transform.position, priority: 1,
                        onCreated: fx => { if (bcard != null) fx.AddComponent<SnapFX>().target = bcard.transform; }, ownerPlayerId: caster.player_id);
                    AudioTool.Get().PlaySFX("ability", iability.cast_audio);
                }
            }
        }

        private void OnAbilityAfter(AbilityData iability, Card caster)
        {
            if (iability != null && caster != null)
            {
                if (caster.uid == bcard.GetCardUID())
                {

                }
            }
        }

        private void OnAbilityEffect(AbilityData iability, Card caster, Card target)
        {
            if (iability != null && caster != null && target != null)
            {
                if (target.uid == bcard.GetCardUID())
                {
                    // Target FX is prepared by Presentation before the state is published.
                }

                if (caster.uid == bcard.GetCardUID())
                {
                    if (iability.charge_target && caster.CardData.IsBoardCard())
                    {
                        BoardCard btarget = BoardCard.Get(target.uid);
                        ChargeInto(btarget);
                    }
                }
            }
        }

        private void OnStatChange(Card target, EffectStatType type)
        {
            Card card = bcard.GetCard();

            if (target != null && target.uid == card.uid)
            {
                if (type == EffectStatType.Attack)
                {
                    attack.DOPunchScale(Vector3.one, 0.5f, 7, 1f);
                }

                if (type == EffectStatType.HP)
                {
                    health.DOPunchScale(Vector3.one, 0.5f, 7, 1f);
                }
                
                if (type == EffectStatType.Range)
                {
                    range.DOPunchScale(Vector3.one, 0.5f, 7, 1f);
                }
            }
        }

        //Card art, canvases and the FX attached to the card move together; orders are kept
        public void SetSortingOrder(string layer_id)
        {
            SortingLayerBase.Get(bcard.gameObject).Apply(layer_id);
        }
    }
}
