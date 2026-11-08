namespace WorldTree.Core.Economy
{
    /// <summary>
    /// Counters that reset at local midnight (GDD §5): Moonpetal Sap refills (escalating cost) and
    /// rewarded-ad Sap claims (daily cap).
    /// </summary>
    public sealed class DailyLimits
    {
        public DailyLimits(int? day, int sapRefills, int adSapClaims)
        {
            Day = day;
            SapRefills = sapRefills < 0 ? 0 : sapRefills;
            AdSapClaims = adSapClaims < 0 ? 0 : adSapClaims;
        }

        public int? Day { get; private set; }
        public int SapRefills { get; private set; }
        public int AdSapClaims { get; private set; }

        /// <summary>Resets the counters when a new local day has started.</summary>
        public void RollOver(int today)
        {
            if (Day == today) return;
            Day = today;
            SapRefills = 0;
            AdSapClaims = 0;
        }

        internal void CountSapRefill() => SapRefills++;
        internal void CountAdSapClaim() => AdSapClaims++;
    }
}
