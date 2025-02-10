using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that removes a status,
    /// Will remove all status if the public field is empty
    /// </summary>

    public enum EffectStatusType
    {
        BadStatus = 0,
        GoodStatus = 1,
        BothStatus = 2,
    }

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/ClearStatus", order = 10)]
    public class EffectClearStatus : EffectData
    {
        public StatusData status;
        public EffectStatusType status_type;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            if (status != null)
                target.RemoveStatus(status.effect);
            else
            {
                foreach (CardStatus sta in target.GetAllStatus())
                {
                    StatusData istatus = StatusData.Get(sta.type);

                    if (istatus.bad_status && (status_type == EffectStatusType.BadStatus || status_type == EffectStatusType.BothStatus))
                        target.RemoveStatus(sta.type);

                    if (!istatus.bad_status && (status_type == EffectStatusType.GoodStatus || status_type == EffectStatusType.BothStatus))
                        target.RemoveStatus(sta.type);
                }
            }

        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            if (status != null)
                target.RemoveStatus(status.effect);
            else
            {
                foreach (CardStatus sta in target.GetAllStatus())
                {
                    StatusData istatus = StatusData.Get(sta.type);

                    if (istatus.bad_status && (status_type == EffectStatusType.BadStatus || status_type == EffectStatusType.BothStatus))
                        target.RemoveStatus(sta.type);

                    if (!istatus.bad_status && (status_type == EffectStatusType.GoodStatus || status_type == EffectStatusType.BothStatus))
                        target.RemoveStatus(sta.type);
                }
            }
        }
    }
}