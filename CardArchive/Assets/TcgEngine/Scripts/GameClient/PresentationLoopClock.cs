using System;
using UnityEngine;

namespace TcgEngine.Client
{
    // Session-wide decorative time. Independent of per-step completion/gates.
    public static class PresentationLoopClock
    {
        private static double origin;
        public static double Elapsed => Math.Max(0d, Time.timeAsDouble - origin);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetSession() { origin = Time.timeAsDouble; }

        public static float Phase(float period)
        {
            return period > 0f ? (float)((Elapsed / period) % 1d) : 0f;
        }
    }
}
