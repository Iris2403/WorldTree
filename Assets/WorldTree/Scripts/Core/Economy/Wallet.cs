using System;

namespace WorldTree.Core.Economy
{
    /// <summary>
    /// Currencies (GDD §9). Glimmer (soft) arrived in Phase 2 for selling and discoveries (roadmap D27);
    /// Moonpetals (premium) in Phase 3 for refresh skips and Sap refills (D32). Lifebloom and XP arrive in Phase 4.
    /// </summary>
    public sealed class Wallet
    {
        public Wallet(int glimmer, int moonpetals = 0)
        {
            if (glimmer < 0) throw new ArgumentOutOfRangeException(nameof(glimmer));
            if (moonpetals < 0) throw new ArgumentOutOfRangeException(nameof(moonpetals));
            Glimmer = glimmer;
            Moonpetals = moonpetals;
        }

        public int Glimmer { get; private set; }
        public int Moonpetals { get; private set; }

        public void AddMoonpetals(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Moonpetals += amount;
        }

        /// <summary>Atomic: on failure nothing changes.</summary>
        public bool TrySpendMoonpetals(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (Moonpetals < amount) return false;
            Moonpetals -= amount;
            return true;
        }

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
