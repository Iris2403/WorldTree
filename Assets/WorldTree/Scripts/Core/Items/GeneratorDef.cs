using System;

namespace WorldTree.Core.Items
{
    /// <summary>
    /// Static definition of a generator level (GDD §4.1–4.2). A permanent generator's first tap on a
    /// full generator starts a fixed refresh timer of <see cref="RefreshDuration"/>; when it ends the
    /// generator is full again, however many charges are left. A consumable generator (Mystery Acorn)
    /// has a fixed number of taps, never refreshes, and vanishes when empty.
    /// </summary>
    public sealed class GeneratorDef
    {
        public GeneratorDef(int capacity, TimeSpan refreshDuration, int sapCost, DropTable drops)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (refreshDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(refreshDuration));
            if (sapCost < 0) throw new ArgumentOutOfRangeException(nameof(sapCost));
            Capacity = capacity;
            RefreshDuration = refreshDuration;
            SapCost = sapCost;
            Drops = drops ?? throw new ArgumentNullException(nameof(drops));
        }

        /// <summary>Creates a consumable generator: <paramref name="taps"/> taps, no refresh, vanishes when empty.</summary>
        public static GeneratorDef Consumable(int taps, int sapCost, DropTable drops) =>
            new GeneratorDef(taps, TimeSpan.MaxValue, sapCost, drops, isConsumable: true);

        private GeneratorDef(int capacity, TimeSpan refreshDuration, int sapCost, DropTable drops, bool isConsumable)
            : this(capacity, refreshDuration, sapCost, drops)
        {
            IsConsumable = isConsumable;
        }

        /// <summary>Charges when full (permanent) or total taps (consumable).</summary>
        public int Capacity { get; }

        public bool IsConsumable { get; }
        public TimeSpan RefreshDuration { get; }
        public int SapCost { get; }
        public DropTable Drops { get; }
    }
}
