using System;
using System.Collections.Generic;
using WorldTree.Core.Common;
using WorldTree.Core.Items;

namespace WorldTree.Core.Orders
{
    /// <summary>
    /// Procedural orders (GDD §6.2).
    /// <list type="bullet">
    /// <item><b>Feasibility:</b> only chains the player can currently generate are requested.</item>
    /// <item><b>Demand rule:</b> with two or more chains available, the next order is the candidate that leaves the
    /// least demand (level-1 equivalents, including the new order) among the last <see cref="OrderRules.DemandWindow"/>
    /// orders, never more than <see cref="OrderRules.MaxConsecutiveSameChain"/> in a row. Over the order stream this keeps
    /// every chain at or below <see cref="OrderRules.MaxChainShare"/> (roadmap D37).</item>
    /// <item><b>Levels:</b> around the player's "comfort level" in that chain (the highest level discovered),
    /// occasionally one level higher as a stretch.</item>
    /// </list>
    /// </summary>
    public static class OrderGenerator
    {
        public sealed class Context
        {
            /// <summary>Item chains the player can generate right now.</summary>
            public IReadOnlyList<ChainDef> AvailableChains;

            /// <summary>Highest discovered level per chain ID (at least 1).</summary>
            public Func<string, int> ComfortLevel;

            /// <summary>Demand per chain ID over the last <see cref="OrderRules.DemandWindow"/> orders, in level-1 equivalents.</summary>
            public Func<string, int> RecentDemand;

            /// <summary>Total demand over the last <see cref="OrderRules.DemandWindow"/> orders, all chains.</summary>
            public int RecentTotal;

            /// <summary>
            /// Optional. Unfulfilled demand of the active orders per chain, and what the chain's generators can
            /// still produce right now, both in level-1 equivalents. When given, orders the generators can't
            /// currently cover are avoided (roadmap D38).
            /// </summary>
            public Func<string, int> OpenDemand;
            public Func<string, int> ProducibleNow;

            /// <summary>Chains of the most recent orders, newest last.</summary>
            public IReadOnlyList<string> RecentChains;

            public int PlayerLevel;
            public int OrderId;
            public int CompletedOrders;
        }

        /// <summary>Returns a new order, or null if the player has no generator to fulfil one with.</summary>
        public static Order Generate(Context c, IRandom random)
        {
            if (c.AvailableChains == null || c.AvailableChains.Count == 0) return null;
            string blocked = c.AvailableChains.Count > 1 ? BlockedChain(c) : null;

            // Roll a candidate for every allowed chain, then keep the one that leaves recent demand most
            // balanced after it is added (least demand + value). Accounting for the order's own size is what
            // keeps the long-run share under the cap; picking the chain first and sizing afterwards doesn't.
            ChainDef bestChain = null;
            List<OrderRequirement> bestItems = null;
            int bestValue = 0;
            int bestScore = int.MaxValue;
            int ties = 0;
            foreach (var chain in c.AvailableChains)
            {
                if (chain.Id == blocked) continue;
                var items = RollItems(c, chain, random, out int value);
                int demand = c.RecentDemand(chain.Id);
                // Cap guard: if this order would push the chain over the cap within the window, re-roll a few
                // times and keep the smallest candidate (roadmap D37).
                for (int attempt = 0; attempt < 3 && c.AvailableChains.Count > 1
                     && demand + value > OrderRules.MaxChainShare * (c.RecentTotal + value); attempt++)
                {
                    var retry = RollItems(c, chain, random, out int retryValue);
                    if (retryValue < value)
                    {
                        items = retry;
                        value = retryValue;
                    }
                }
                int score = demand + value;
                // Prefer orders the player's generators can cover now, together with the other open orders on
                // that chain: an order-driven player then never runs every needed generator dry (roadmap D38).
                if (c.OpenDemand != null && c.ProducibleNow != null && c.OpenDemand(chain.Id) + value > c.ProducibleNow(chain.Id))
                    score += Uncoverable;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestChain = chain;
                    bestItems = items;
                    bestValue = value;
                    ties = 1;
                }
                else if (score == bestScore && random.Next(++ties) == 0)
                {
                    bestChain = chain;
                    bestItems = items;
                    bestValue = value;
                }
            }

            string customer = OrderRules.Customers[c.OrderId % OrderRules.Customers.Length];
            string bonus = (c.CompletedOrders + 1) % OrderRules.BonusEveryNthOrder == 0 ? OrderRules.BonusItemId : null;
            return new Order(c.OrderId, customer, bestChain.Id, bestItems,
                OrderRules.GlimmerFor(bestValue), OrderRules.XpFor(bestValue), OrderRules.LifebloomFor(bestValue), bonus);
        }

        private const int Uncoverable = 1_000_000;

        private static List<OrderRequirement> RollItems(Context c, ChainDef chain, IRandom random, out int value)
        {
            int count = 1 + random.Next(OrderRules.MaxItemsForLevel(c.PlayerLevel));
            int comfort = Math.Max(1, Math.Min(c.ComfortLevel(chain.Id), chain.Levels.Count));
            var items = new List<OrderRequirement>(count);
            value = 0;
            for (int i = 0; i < count; i++)
            {
                int low = Math.Max(1, comfort - 2);
                int level = low + random.Next(comfort - low + 1);
                if (random.Next(100) < 15 && level == comfort && comfort < chain.Levels.Count) level++; // stretch request
                var item = chain.Levels[level - 1];
                items.Add(new OrderRequirement(item.Id));
                value += OrderRules.Value(item);
            }
            return items;
        }

        /// <summary>The chain that supplied the last N orders in a row (max N in a row), or null.</summary>
        private static string BlockedChain(Context c)
        {
            int n = OrderRules.MaxConsecutiveSameChain;
            if (c.RecentChains == null || c.RecentChains.Count < n) return null;
            string last = c.RecentChains[c.RecentChains.Count - 1];
            for (int i = c.RecentChains.Count - n; i < c.RecentChains.Count; i++)
                if (c.RecentChains[i] != last) return null;
            return last;
        }
    }
}
