using System;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>
    /// The shared clock and seeded randomness of the v4 package (D-60, D-61; prototype g101-clock.js). Looping and timed
    /// presentation reads <see cref="Seconds"/> instead of <c>Time.time</c>, so a state opened twice with the same seed and
    /// freeze time renders the same frame: <see cref="Freeze"/> pins the clock to the canonical instant 100000 + ft ms.
    /// Game rules never read this clock (the server owns game time).
    /// </summary>
    public static class GameClock
    {
        /// <summary>Canonical freeze origin (D-61): every animation sits at 100000 + ft ms when frozen.</summary>
        public const double CanonMs = 100000;
        private static double startRealtime = -1;
        private static double? frozenAtMs;
        private static uint seed = 1;
        private static Mulberry32 main = new Mulberry32(1);

        public static bool Frozen => frozenAtMs.HasValue;
        public static uint Seed => seed;

        /// <summary>Virtual milliseconds since the clock started (or the frozen instant).</summary>
        public static double NowMs
        {
            get
            {
                if (frozenAtMs.HasValue) return frozenAtMs.Value;
                if (startRealtime < 0) startRealtime = Time.realtimeSinceStartupAsDouble;
                return (Time.realtimeSinceStartupAsDouble - startRealtime) * 1000.0;
            }
        }
        public static float Seconds => (float)(NowMs / 1000.0);

        /// <summary>Restart the clock and the seeded stream (prototype: page load with ?seed=N).</summary>
        public static void Reset(uint newSeed)
        {
            seed = newSeed; main = new Mulberry32(newSeed); frozenAtMs = null;
            startRealtime = Time.realtimeSinceStartupAsDouble;
            Time.timeScale = 1;
        }

        /// <summary>Pin the clock at CanonMs + ftMs and stop frame-time animation (prototype G101Clock.freeze).</summary>
        public static void Freeze(double ftMs)
        {
            if (frozenAtMs.HasValue) return;
            frozenAtMs = CanonMs + Math.Max(0, ftMs);
            Time.timeScale = 0;
        }

        /// <summary>Next value of the main seeded stream in [0, 1) (prototype Math.random after g101-clock.js).</summary>
        public static double Random() => main.NextDouble();

        /// <summary>An independent named stream: the same seed and name give the same sequence (G101Clock.rng).</summary>
        public static Mulberry32 Stream(string name)
        {
            uint h = seed ^ 0x9E3779B9u;
            foreach (char c in name ?? "") h = unchecked((h ^ c) * 0x01000193u);
            return new Mulberry32(h);
        }
    }

    /// <summary>mulberry32, bit-for-bit the prototype's generator (g101-clock.js, D-02).</summary>
    public sealed class Mulberry32
    {
        private uint a;
        public Mulberry32(uint seed) { a = seed; }
        public uint NextUInt()
        {
            unchecked
            {
                a += 0x6D2B79F5u;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return t ^ (t >> 14);
            }
        }
        public double NextDouble() => NextUInt() / 4294967296.0;
    }
}
