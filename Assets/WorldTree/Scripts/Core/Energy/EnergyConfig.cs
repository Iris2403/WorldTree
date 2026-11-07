using System;

namespace WorldTree.Core.Energy
{
    /// <summary>Sap tuning (GDD §5): cap 100, +1 every 120 s while below the cap.</summary>
    public sealed class EnergyConfig
    {
        public EnergyConfig(int cap, TimeSpan regenInterval)
        {
            if (cap <= 0) throw new ArgumentOutOfRangeException(nameof(cap));
            if (regenInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(regenInterval));
            Cap = cap;
            RegenInterval = regenInterval;
        }

        public int Cap { get; }
        public TimeSpan RegenInterval { get; }

        public static EnergyConfig Default => new EnergyConfig(100, TimeSpan.FromSeconds(120));
    }
}
