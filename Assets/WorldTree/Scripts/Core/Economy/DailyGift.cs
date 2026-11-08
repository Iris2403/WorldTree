using System;
using System.Collections.Generic;

namespace WorldTree.Core.Economy
{
    /// <summary>
    /// The 7-day Daily Gift calendar (GDD §5). One claim per local day. Missing a day doesn't reset
    /// progress; the calendar just waits. After day 7 it starts again at day 1.
    /// </summary>
    public sealed class DailyGift
    {
        public DailyGift(int nextDayIndex, int? lastClaimDay)
        {
            NextDayIndex = ((nextDayIndex % Days) + Days) % Days;
            LastClaimDay = lastClaimDay;
        }

        public const int Days = 7;

        /// <summary>0-based index of the next gift to claim (0 = day 1).</summary>
        public int NextDayIndex { get; private set; }

        /// <summary>Local day number of the last claim (see <see cref="Common.LocalDay"/>), or null if never claimed.</summary>
        public int? LastClaimDay { get; private set; }

        public bool CanClaim(int today) => LastClaimDay != today;

        internal void MarkClaimed(int today)
        {
            LastClaimDay = today;
            NextDayIndex = (NextDayIndex + 1) % Days;
        }

        internal void DebugAllowClaimAgain() => LastClaimDay = null;

        /// <summary>The rewards for each calendar day (roadmap D35). Day 7 is the big one, with Moonpetals.</summary>
        public static IReadOnlyList<Reward> RewardsFor(int dayIndex)
        {
            if (dayIndex < 0 || dayIndex >= Days) throw new ArgumentOutOfRangeException(nameof(dayIndex));
            return Calendar[dayIndex];
        }

        private static readonly Reward[][] Calendar =
        {
            new[] { Reward.Sap(30) },
            new[] { Reward.Glimmer(25) },
            new[] { Reward.Item("gen.seed_pouch.1") },
            new[] { Reward.Sap(50) },
            new[] { Reward.Item("gen.mystery_acorn") },
            new[] { Reward.Item("gen.moonwell_spring.1") },
            new[] { Reward.Moonpetals(20), Reward.Sap(50) },
        };
    }
}
