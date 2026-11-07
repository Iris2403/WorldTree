using System;
using WorldTree.Core.Common;

namespace WorldTree.Tests.Support
{
    public sealed class FakeClock : IClock
    {
        public FakeClock(DateTime start)
        {
            UtcNow = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        }

        public DateTime UtcNow { get; set; }

        public static FakeClock AtNoon() => new FakeClock(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        public void Advance(TimeSpan by) => UtcNow += by;
        public void AdvanceSeconds(double seconds) => UtcNow += TimeSpan.FromSeconds(seconds);
        public void AdvanceMinutes(double minutes) => UtcNow += TimeSpan.FromMinutes(minutes);
    }
}
