using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using TcgEngine.UI;
using UnityEngine;

namespace TcgEngine.FX
{
    /// <summary>
    /// 타일에 적용되는 모든 이펙트
    /// </summary>
    /// 
    /// 
    public class BoardSlotFX : MonoBehaviour
    {
        private BoardSlot bslot;
        private Animator bslot_animator;

        void Awake()
        {
            bslot = GetComponent<BoardSlot>();
            bslot_animator = GetComponent<Animator>();
        }

        void Start()
        {
            GameClient client = GameClient.Get();

            client.onAbilityStart += OnAbilityStart;
            client.onAbilityTargetSlot += OnAbilityEffect;
            client.onAbilityEnd += OnAbilityAfter;
        }

        private void OnDestroy()
        {
            GameClient client = GameClient.Get();

            client.onAbilityStart -= OnAbilityStart;
            client.onAbilityTargetSlot -= OnAbilityEffect;
            client.onAbilityEnd -= OnAbilityAfter;
        }

        void Update()
        {

        }

        private void OnAbilityStart(AbilityData iability, Card caster)
        {
            if (iability != null && caster != null)
            {

            }
        }

        private void OnAbilityAfter(AbilityData iability, Card caster)
        {
            if (iability != null && caster != null)
            {

            }
        }

        private void OnAbilityEffect(AbilityData iability, Card caster, Slot target)
        {
            if (iability != null && caster != null && target != null)
            {
                if (target == bslot.GetSlot())
                {
                    FXTool.DoSnapFX(iability.target_fx, bslot.transform);
                    AudioTool.Get().PlaySFX("ability_effect", iability.target_audio);
                }
                /*
                if (caster.uid == bcard.GetCardUID())
                {
                    if (iability.charge_target && caster.CardData.IsBoardCard())
                    {
                        BoardCard btarget = BoardCard.Get(target.uid);
                        ChargeInto(btarget);
                    }
                }
                */
            }
        }


        public void SetAnimParameter(bool is_selected)
        {
            bslot_animator.SetBool("is_selected", is_selected);
        }

        public void SetSortingLayer(string layer_id)
        {
            SpriteRenderer[] spriteRenderers = bslot.GetComponentsInChildren<SpriteRenderer>();

            foreach (SpriteRenderer sr in spriteRenderers)
            {
                sr.sortingLayerName = layer_id;
            }
        }

        public void ResetIndicator()
        {
            SetAnimParameter(false);
            SetSortingLayer("Default");
        }
    }
}
