using System;
using System.Globalization;

namespace TcgEngine.Replay
{
    // Records rolls even when they cause no state change (failed probability checks).
    // Playback never constructs or calls this RNG.
    public sealed class ReplayRandom : Random
    {
        readonly Action<string> record;
        public ReplayRandom(Action<string> record) { this.record = record; }
        public override int Next() { int v = base.Next(); record?.Invoke("next:" + v); return v; }
        public override int Next(int max) { int v = base.Next(max); record?.Invoke("next:" + max + ":" + v); return v; }
        public override int Next(int min, int max) { int v = base.Next(min, max); record?.Invoke("next:" + min + ":" + max + ":" + v); return v; }
        public override double NextDouble() { double v = base.NextDouble(); record?.Invoke("double:" + v.ToString("R", CultureInfo.InvariantCulture)); return v; }
        public override void NextBytes(byte[] buffer) { base.NextBytes(buffer); record?.Invoke("bytes:" + Convert.ToBase64String(buffer)); }
    }
}
