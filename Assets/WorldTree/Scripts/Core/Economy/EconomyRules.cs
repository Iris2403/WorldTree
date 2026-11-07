using System;

namespace WorldTree.Core.Economy
{
    /// <summary>Phase 2 tuning constants (GDD §3.4–3.5). Moved into data when Phase 4 adds the economy assets.</summary>
    public static class EconomyRules
    {
        /// <summary>First discovery of an item pays this much Glimmer per item level.</summary>
        public const int DiscoveryGlimmerPerLevel = 2;

        /// <summary>Satchel slots at the start (GDD §3.5).</summary>
        public const int StartingSatchelSlots = 4;

        /// <summary>How long the last sale can be undone (GDD §3.5).</summary>
        public static readonly TimeSpan SellUndoWindow = TimeSpan.FromSeconds(5);
    }
}
