using System;

namespace WorldTree.Core.Economy
{
    /// <summary>
    /// Soft currency (Glimmer, GDD §9). Introduced in Phase 2 because selling and first-discovery
    /// rewards pay Glimmer (roadmap D27). Lifebloom, Moonpetals and XP arrive in Phase 4.
    /// </summary>
    public sealed class Wallet
    {
        public Wallet(int glimmer)
        {
            if (glimmer < 0) throw new ArgumentOutOfRangeException(nameof(glimmer));
            Glimmer = glimmer;
        }

        public int Glimmer { get; private set; }

        public void AddGlimmer(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Glimmer += amount;
        }

        /// <summary>Atomic: on failure nothing changes.</summary>
        public bool TrySpendGlimmer(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (Glimmer < amount) return false;
            Glimmer -= amount;
            return true;
        }
    }
}
