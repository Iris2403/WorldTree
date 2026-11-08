using System;

namespace WorldTree.Core.Economy
{
    /// <summary>Tuning constants (GDD §3.4–3.5, §4.1, §5). Moved into data when Phase 4 adds the economy assets.</summary>
    public static class EconomyRules
    {
        /// <summary>First discovery of an item pays this much Glimmer per item level.</summary>
        public const int DiscoveryGlimmerPerLevel = 2;

        /// <summary>Satchel slots at the start (GDD §3.5).</summary>
        public const int StartingSatchelSlots = 4;

        /// <summary>How long the last sale can be undone (GDD §3.5).</summary>
        public static readonly TimeSpan SellUndoWindow = TimeSpan.FromSeconds(5);

        /// <summary>Skipping a refresh costs 1 Moonpetal per started 5 minutes left (20 min = 4), minimum 1 (roadmap D34).</summary>
        public const int SkipMinutesPerMoonpetal = 5;

        /// <summary>A Moonpetal Sap refill adds this much Sap (GDD §5).</summary>
        public const int SapRefillAmount = 100;

        /// <summary>The first refill of a local day costs this; each further refill that day doubles it (GDD §5).</summary>
        public const int SapRefillBaseCost = 10;

        /// <summary>A rewarded ad gives this much Sap, at most <see cref="AdSapDailyCap"/> times per local day (GDD §5).</summary>
        public const int AdSapAmount = 25;
        public const int AdSapDailyCap = 5;

        public static int SkipCost(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero) return 0;
            int cost = (int)Math.Ceiling(remaining.TotalMinutes / SkipMinutesPerMoonpetal);
            return Math.Max(1, cost);
        }

        public static int SapRefillCost(int refillsAlreadyToday) => SapRefillBaseCost << Math.Min(refillsAlreadyToday, 16);
    }
}
