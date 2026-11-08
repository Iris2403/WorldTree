using System;

namespace WorldTree.Core.Progression
{
    /// <summary>Player level and XP (GDD §7.1).</summary>
    public sealed class PlayerProgress
    {
        public PlayerProgress(int level, int xp)
        {
            Level = Math.Max(1, level);
            Xp = Math.Max(0, xp);
        }

        public int Level { get; private set; }

        /// <summary>XP collected towards the next level.</summary>
        public int Xp { get; private set; }

        public int XpToNextLevel => XpRequired(Level);

        /// <summary>XP needed to go from <paramref name="level"/> to the next (roadmap D36).</summary>
        public static int XpRequired(int level) => 15 * level;

        /// <summary>Adds XP and returns how many levels were gained.</summary>
        internal int AddXp(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Xp += amount;
            int gained = 0;
            while (Xp >= XpRequired(Level))
            {
                Xp -= XpRequired(Level);
                Level++;
                gained++;
            }
            return gained;
        }

        /// <summary>Glimmer for reaching <paramref name="level"/>.</summary>
        public static int LevelUpGlimmer(int level) => 10 * level;

        /// <summary>Moonpetals for each level-up (GDD §9: small amounts from level-ups).</summary>
        public const int LevelUpMoonpetals = 2;
    }
}
