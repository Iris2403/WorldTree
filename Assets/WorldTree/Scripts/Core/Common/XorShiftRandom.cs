using System;

namespace WorldTree.Core.Common
{
    /// <summary>
    /// Small, fast, seedable PRNG (xorshift32). Its whole state is one <see cref="uint"/>, which is
    /// saved with the game so that generator drops continue deterministically after a reload.
    /// </summary>
    public sealed class XorShiftRandom : IRandom
    {
        private const uint FallbackSeed = 0x9E3779B9u;
        private uint _state;

        public XorShiftRandom(uint seed)
        {
            _state = seed == 0 ? FallbackSeed : seed;
        }

        /// <summary>The current state. Restoring it with the constructor resumes the same sequence.</summary>
        public uint State => _state;

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return (int)(x % (uint)maxExclusive);
        }
    }
}
