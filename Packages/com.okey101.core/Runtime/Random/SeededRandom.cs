using System;

namespace Okey101.Core.Random
{
    public sealed class SeededRandom
    {
        public const string AlgorithmVersion = "xorshift32-fy-v1";
        private uint state;
        public SeededRandom(uint seed) { state = seed == 0 ? 0x6D2B79F5u : seed; }
        public uint NextUInt()
        {
            unchecked
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }
        }
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            var bound = (uint)exclusiveMax;
            // Xorshift32 excludes zero, so map its 2^32 - 1 outputs to [0, 2^32 - 2].
            var limit = uint.MaxValue - (uint.MaxValue % bound);
            uint value;
            do { value = NextUInt() - 1u; } while (value >= limit);
            return (int)(value % bound);
        }
    }
}
