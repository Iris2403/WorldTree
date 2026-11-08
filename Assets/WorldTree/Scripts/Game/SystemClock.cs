using System;
using WorldTree.Core.Common;

namespace WorldTree.Game
{
    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public TimeSpan LocalUtcOffset => TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
    }
}
