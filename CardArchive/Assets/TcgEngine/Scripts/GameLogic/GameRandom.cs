using System;

namespace TcgEngine
{
    // Explicit state allows predictions to fork randomness without consuming the live stream.
    public class GameRandom : Random
    {
        private ulong state;
        public GameRandom() : this(Environment.TickCount) { }
        public GameRandom(int seed) { state = unchecked((uint)seed) + 1UL; }
        private GameRandom(ulong state) { this.state = state; }
        public GameRandom Clone() { return new GameRandom(state); }

        protected override double Sample()
        {
            // xorshift64*: nonzero state, 53 random mantissa bits.
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            ulong value = unchecked(state * 2685821657736338717UL);
            return (value >> 11) * (1.0 / 9007199254740992.0);
        }

        public override double NextDouble() { return Sample(); }
        public override int Next() { return (int)(Sample() * int.MaxValue); }
        public override int Next(int maxValue)
        {
            if (maxValue < 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
            return (int)(Sample() * maxValue);
        }
        public override int Next(int minValue, int maxValue)
        {
            if (maxValue < minValue) throw new ArgumentOutOfRangeException(nameof(maxValue));
            return (int)(minValue + (long)(Sample() * ((long)maxValue - minValue)));
        }
        public override void NextBytes(byte[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            for (int i = 0; i < buffer.Length; i++) buffer[i] = (byte)(Sample() * 256);
        }
    }
}
