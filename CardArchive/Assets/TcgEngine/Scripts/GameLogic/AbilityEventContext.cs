using System;

namespace TcgEngine
{
    // Additional event data carried by queue entries. Event snapshots must not change after enqueue; AbilityExecutionContext owns mutable invocation state.
    // No card references: descendants use IDs/value snapshots and resolve cards in the current Game.
    [Serializable]
    public abstract class AbilityEventContext
    {
        public abstract AbilityEventContext Clone();
    }
}
