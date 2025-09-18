using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
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

        void Awake()
        {
            bslot = GetComponent<BoardSlot>();
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
    }
}
