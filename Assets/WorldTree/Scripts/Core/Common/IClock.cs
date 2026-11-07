using System;

namespace WorldTree.Core.Common
{
    /// <summary>
    /// Source of the current UTC time. Core never reads the system clock directly, so every
    /// timer rule can be tested with a fake clock (architecture rule 4).
    /// </summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}
