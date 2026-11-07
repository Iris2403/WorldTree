using System;

namespace WorldTree.Core.Generators
{
    /// <summary>
    /// Mutable charge state of one generator instance. It belongs to the item, not the cell, so it
    /// travels with the generator when it is moved or swapped (GDD §4.3).
    /// </summary>
    public sealed class GeneratorState
    {
        public GeneratorState(int charges, DateTime? refreshEndsUtc)
        {
            if (charges < 0) throw new ArgumentOutOfRangeException(nameof(charges));
            Charges = charges;
            RefreshEndsUtc = refreshEndsUtc;
        }

        public int Charges { get; internal set; }

        /// <summary>End of the running refresh cycle, or null when the generator is full and idle.</summary>
        public DateTime? RefreshEndsUtc { get; internal set; }
    }
}
