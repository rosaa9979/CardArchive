using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Boss-only runtime state for Total Assault matches. Holds the three boss
    /// gauges (Skill / ATG / Groggy) and per-boss flags. Lives on Game.boss_state,
    /// null for non-boss matches. Created from TotalAssaultData at match start.
    /// </summary>
    [System.Serializable]
    public class BossState
    {
        public int player_id = -1;

        public int skill_gauge;
        public int skill_gauge_max;
        public int atg_gauge;
        public int atg_gauge_max;
        public int groggy_gauge;
        public int groggy_gauge_max;

        public bool mana_increases_per_turn = true;
        public bool skip_next_turn = false;

        public int GetGauge(BossGaugeType type)
        {
            switch (type)
            {
                case BossGaugeType.Skill:  return skill_gauge;
                case BossGaugeType.Atg:    return atg_gauge;
                case BossGaugeType.Groggy: return groggy_gauge;
            }
            return 0;
        }

        public int GetGaugeMax(BossGaugeType type)
        {
            switch (type)
            {
                case BossGaugeType.Skill:  return skill_gauge_max;
                case BossGaugeType.Atg:    return atg_gauge_max;
                case BossGaugeType.Groggy: return groggy_gauge_max;
            }
            return 0;
        }

        public void SetGauge(BossGaugeType type, int value)
        {
            int max = GetGaugeMax(type);
            value = Mathf.Clamp(value, 0, max);

            switch (type)
            {
                case BossGaugeType.Skill:
                    skill_gauge = value;
                    break;
                case BossGaugeType.Atg:
                    atg_gauge = value;
                    break;
                case BossGaugeType.Groggy:
                    groggy_gauge = value;
                    //Groggy at max → schedule next-turn skip + reset
                    if (groggy_gauge_max > 0 && groggy_gauge >= groggy_gauge_max)
                    {
                        skip_next_turn = true;
                        groggy_gauge = 0;
                    }
                    break;
            }
        }

        public void AddGauge(BossGaugeType type, int delta)
        {
            SetGauge(type, GetGauge(type) + delta);
        }
    }

    public enum BossGaugeType
    {
        Skill = 0,
        Atg = 10,
        Groggy = 20,
    }
}
