using System;

namespace WorldTree.Core.Items
{
    /// <summary>
    /// Static definition of a permanent generator level (GDD §4.1–4.2). The first tap on a full
    /// generator starts a fixed refresh timer of <see cref="RefreshDuration"/>; when it ends the
    /// generator is full again, however many charges are left.
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

        public int Capacity { get; }
        public TimeSpan RefreshDuration { get; }
        public int SapCost { get; }
        public DropTable Drops { get; }
    }
}
