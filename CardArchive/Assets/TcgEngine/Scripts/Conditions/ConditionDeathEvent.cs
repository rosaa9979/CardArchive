using System;
using UnityEngine;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/DeathEvent", order = 10)]
    public class ConditionDeathEvent : ConditionData
    {
        [Tooltip("Empty accepts any death cause.")]
        public DeathCause[] causes = Array.Empty<DeathCause>();
        public ConditionData[] victim_conditions = Array.Empty<ConditionData>();
        public ConditionData[] killer_conditions = Array.Empty<ConditionData>();

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster, AbilityEventContext context = null)
        {
            DeathEventContext death = context as DeathEventContext;
            return death != null
                && (causes == null || causes.Length == 0 || Array.IndexOf(causes, death.cause) >= 0)
                && Matches(victim_conditions, data, ability, caster, data.GetCard(death.victim_uid), context)
                && Matches(killer_conditions, data, ability, caster, data.GetCard(death.killer_uid), context);
        }

        private static bool Matches(ConditionData[] conditions, Game data, AbilityData ability, Card caster, Card subject, AbilityEventContext context)
        {
            if (conditions == null) return true;
            foreach (ConditionData condition in conditions)
            {
                if (condition != null && (subject == null
                    || !condition.IsTriggerConditionMet(data, ability, caster, context)
                    || !condition.IsTargetConditionMet(data, ability, caster, subject, context)))
                    return false;
            }
            return true;
        }
    }
}
