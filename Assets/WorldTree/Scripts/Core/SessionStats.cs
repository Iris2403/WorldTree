using System;

namespace WorldTree.Core
{
    /// <summary>
    /// Per-play-session counters for the debug overlay, used to measure the GDD §5.1 "seconds per Sap"
    /// assumption. Not saved: each app launch starts a new measurement.
    /// </summary>
    public sealed class SessionStats
    {
        public SessionStats(DateTime startedUtc)
        {
            StartedUtc = startedUtc;
        }

        public DateTime StartedUtc { get; private set; }
        public int Taps { get; internal set; }
        public int Merges { get; internal set; }
        public int Moves { get; internal set; }
        public int Swaps { get; internal set; }
        public int SapSpent { get; internal set; }

        /// <summary>Average seconds of play per Sap spent, or null before the first Sap is spent.</summary>
        public double? SecondsPerSap(DateTime nowUtc) =>
            SapSpent == 0 ? (double?)null : (nowUtc - StartedUtc).TotalSeconds / SapSpent;

        public void Reset(DateTime nowUtc)
        {
            StartedUtc = nowUtc;
            Taps = Merges = Moves = Swaps = SapSpent = 0;
        }
    }
}
