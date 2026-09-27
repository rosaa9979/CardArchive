using System;

namespace TcgEngine
{
    public enum DeathCause
    {
        Unknown = 0,
        AttackDamage = 1,
        CounterAttackDamage = 2,
        AbilityDamage = 3,
        DestroyEffect = 4,
    }

    [Serializable]
    public sealed class DeathEventContext : AbilityEventContext
    {
        public readonly string victim_uid;
        public readonly string killer_uid;
        public readonly Slot slot;
        public readonly DeathCause cause;
        public readonly bool counter_attack;

        public DeathEventContext(Card victim)
            : this(victim.uid, victim.death_source_uid, victim.slot, victim.death_cause, victim.death_source_counter) { }

        public DeathEventContext(string victimUid, string killerUid, Slot deathSlot, DeathCause deathCause, bool counterAttack)
        {
            victim_uid = victimUid;
            killer_uid = killerUid;
            slot = deathSlot;
            cause = deathCause;
            counter_attack = counterAttack;
        }

        public override AbilityEventContext Clone()
        {
            return new DeathEventContext(victim_uid, killer_uid, slot, cause, counter_attack);
        }
    }
}
