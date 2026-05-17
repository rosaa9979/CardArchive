using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Modifies a boss gauge (Skill / ATG / Groggy). Either adds delta to the
    /// current value, or sets it directly. No-op when the match has no boss state
    /// (i.e. non-Total Assault matches).
    /// Groggy reaching max triggers the boss's next-turn skip automatically
    /// (handled inside BossState.SetGauge).
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/BossGauge", order = 10)]
    public class EffectBossGauge : EffectData
    {
        public BossGaugeType gauge;

        [Tooltip("If true, sets the gauge to set_value. Otherwise adds delta.")]
        public bool set_to_value;
        public int delta;
        public int set_value;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster)
        {
            Apply(logic);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            Apply(logic);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            Apply(logic);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            Apply(logic);
        }

        private void Apply(GameLogic logic)
        {
            BossState state = logic.GetGameData().boss_state;
            if (state == null)
                return;

            if (set_to_value)
                state.SetGauge(gauge, set_value);
            else
                state.AddGauge(gauge, delta);
        }
    }
}
