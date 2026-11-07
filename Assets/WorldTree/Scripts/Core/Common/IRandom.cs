namespace WorldTree.Core.Common
{
    /// <summary>Source of randomness for Core rules. Injected so tests are deterministic.</summary>
    public interface IRandom
    {
        /// <summary>Returns an integer in [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }
}
