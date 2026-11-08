using System;

namespace WorldTree.Core.Common
{
    /// <summary>
    /// Source of the current time. Core never reads the system clock directly, so every timer rule
    /// can be tested with a fake clock (architecture rule 4).
    /// </summary>
    public interface IClock
    {
        DateTime UtcNow { get; }

        /// <summary>
        /// The player's local offset from UTC. Used only for "per local day" rules (Daily Gift, daily
        /// limits that reset at local midnight, GDD §5); all timers use UTC.
        /// </summary>
        TimeSpan LocalUtcOffset { get; }
    }
}
