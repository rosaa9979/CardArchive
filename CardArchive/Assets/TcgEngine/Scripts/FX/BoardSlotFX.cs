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

            TargetSelectorUI.Get().onCurrentSlotChanged += OnCurrentSlotChanged;
        }

        private void OnDestroy()
        {
            GameClient client = GameClient.Get();

            client.onAbilityStart -= OnAbilityStart;
            client.onAbilityTargetSlot -= OnAbilityEffect;
            client.onAbilityEnd -= OnAbilityAfter;

            TargetSelectorUI.Get().onCurrentSlotChanged -= OnCurrentSlotChanged;
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

        public void OnCurrentSlotChanged(TargetSelectorUIType type, BSlot current_slot)
        {
            ResetUX();
            
            if (type == TargetSelectorUIType.None || current_slot == null)
                return;

            Slot slot = current_slot.GetSlot();

            if (slot.IsValid())
            {
                if (type == TargetSelectorUIType.DragHandCard)
                {
                    Card hcard = HandCard.GetDrag().GetCard();

                    if (hcard.CardData.IsCitizen())
                    {
                        List<Slot> range_slots = slot.GetNeighborSlot(hcard.GetRange());

                        if (range_slots.Contains(bslot.GetSlot()))
                                bslot_animator.SetBool("is_selected", true);
                    }
                }
            }
        }

        public void SetAnimParameter(bool is_selected)
        {
            bslot_animator.SetBool("is_selected", is_selected);
        }

        public void ResetUX()
        {
            bslot_animator.SetBool("is_selected", false);
        }
    }
}
