using System;
using WorldTree.Core.Items;

namespace WorldTree.Core.Orders
{
    /// <summary>
    /// Order tuning (GDD §6.2, roadmap D23 and D36). Rewards scale with the order's "merge cost" in
    /// level-1 equivalents, so they feel fair.
    /// </summary>
    public static class OrderRules
    {
        /// <summary>At most this share of order demand may come from one chain (GDD §6.2, configurable).</summary>
        public const double MaxChainShare = 0.55;

        /// <summary>At most this many orders in a row from the same chain (GDD §6.2, configurable).</summary>
        public const int MaxConsecutiveSameChain = 2;

        /// <summary>The demand rule balances chains over this many most recent orders (roadmap D37).</summary>
        public const int DemandWindow = 20;

        public const int BaseSlots = 3;

        /// <summary>The 4th order slot unlocks at this player level (GDD §6.1).</summary>
        public const int FourthSlotLevel = 8;

        /// <summary>Every Nth completed order also gives a bonus item.</summary>
        public const int BonusEveryNthOrder = 5;

        public const string BonusItemId = "gen.mystery_acorn";

        /// <summary>A non-story order can be swapped out once per this interval (GDD §6.1).</summary>
        public static readonly TimeSpan SwapCooldown = TimeSpan.FromMinutes(30);

        /// <summary>Customers from GDD §8.2, cycled in order.</summary>
        public static readonly string[] Customers = { "wren", "thalen", "pip", "marra", "sorrel_fen" };

        public static int SlotsForLevel(int level) => BaseSlots + (level >= FourthSlotLevel ? 1 : 0);

        /// <summary>Level-1 equivalents of an item: a level-k item is worth 2^(k-1) level-1 items.</summary>
        public static int Value(ItemDef item) => 1 << (item.Level - 1);

        public static int GlimmerFor(int value) => value;
        public static int XpFor(int value) => value;
        public static int LifebloomFor(int value) => Math.Max(1, (value + 2) / 4);

        /// <summary>How many items an order may request at a player level (early orders are small).</summary>
        public static int MaxItemsForLevel(int level) => level < 3 ? 1 : level < 6 ? 2 : 3;
    }
}
