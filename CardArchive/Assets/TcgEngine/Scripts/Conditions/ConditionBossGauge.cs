using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Compares a boss gauge (Skill / ATG / Groggy) against a threshold (or its max).
    /// Returns false when the match has no boss state — effectively disables the
    /// hosting ability outside Total Assault.
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/BossGauge", order = 10)]
    public class ConditionBossGauge : ConditionData
    {
        [Header("Boss gauge is")]
        public BossGaugeType gauge;
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;
        public int value;

        [Tooltip("If true, compares the gauge against its current max instead of the value above.")]
        public bool compare_to_max;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            return Check(data);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            return Check(data);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            return Check(data);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            return Check(data);
        }

        private bool Check(Game data)
        {
            if (data.boss_state == null)
                return false;

            int current = data.boss_state.GetGauge(gauge);
            int compare = compare_to_max ? data.boss_state.GetGaugeMax(gauge) : value;
            return CompareInt(current, oper, compare);
        }
    }
}
