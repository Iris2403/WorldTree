using System;

namespace WorldTree.Core.Common
{
    /// <summary>Numbers local calendar days, so "once per day" rules compare a single integer.</summary>
    public static class LocalDay
    {
        /// <summary>Days since 0001-01-01 in the player's local time zone.</summary>
        public static int Today(IClock clock) => (int)((clock.UtcNow + clock.LocalUtcOffset).Ticks / TimeSpan.TicksPerDay);
    }
}
