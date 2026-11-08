using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTree.Core.Common;
using WorldTree.Core.Content;
using WorldTree.Core.Items;
using WorldTree.Core.Orders;
using WorldTree.Tests.Support;

namespace WorldTree.Tests.Balance
{
    /// <summary>Phase 4 balance acceptance (roadmap D22): the C# port matches the Python reference, and the real order generator causes no stalls.</summary>
    public class BalanceTests
    {
        private static IEnumerable<TestCaseData> ReferenceCases() =>
            PythonReference.Totals.Select(r => new TestCaseData(r.Name, r.Spent, r.Regenerated, r.Stalls, r.Stranded, r.Orders).SetName("Reference " + r.Name));

        [TestCaseSource(nameof(ReferenceCases))]
        public void CSharpPortMatchesPythonReference(string name, int spent, int regenerated, int stalls, int stranded, int orders)
        {
            var parts = name.Split('|'); // profile|pattern|strategy|capacities|seconds per Sap
            var caps = parts[3].Split(',').Select(int.Parse).ToArray();
            var t = EnergySimulator.Run(EnergySimulator.Profiles[parts[0]], new EnergySimulator.PatternSource(parts[1]), parts[2], caps, int.Parse(parts[4]));
            Assert.AreEqual(spent, t.Spent, "spent");
            Assert.AreEqual(regenerated, t.Regenerated, "regenerated");
            Assert.AreEqual(stalls, t.Stalls, "stalls");
            Assert.AreEqual(stranded, t.Stranded, "stranded");
            Assert.AreEqual(orders, t.Orders, "orders");
        }

        /// <summary>Feeds the real <see cref="OrderGenerator"/> into the simulator (Nature and Water, two Lv1 generators).</summary>
        internal sealed class RealOrders : EnergySimulator.IOrderSource
        {
            private readonly List<ChainDef> _chains;
            private readonly XorShiftRandom _random;
            private readonly List<string> _recent = new List<string>();
            private readonly List<(string Chain, int Value)> _window = new List<(string, int)>();
            private readonly int _level;
            private readonly int _comfort;
            private int _id;
            public int LastLifebloom { get; private set; }

            public RealOrders(uint seed, int level, int comfort)
            {
                _chains = TestContent.Catalog.Chains.Where(c => c.Id == PrototypeContent.NatureChain || c.Id == PrototypeContent.WaterChain).ToList();
                _random = new XorShiftRandom(seed);
                _level = level;
                _comfort = comfort;
            }

            public (int Gen, int Taps) Next(int[] charges, int[] openTaps)
            {
                // Sim units are taps; a Lv1 tap yields 1.24 level-1 equivalents on average, at least 1.
                int Index(string id) => id == PrototypeContent.NatureChain ? 0 : 1;
                var order = OrderGenerator.Generate(new OrderGenerator.Context
                {
                    AvailableChains = _chains,
                    ComfortLevel = _ => _comfort,
                    RecentDemand = id => _window.Where(w => w.Chain == id).Sum(w => w.Value),
                    RecentTotal = _window.Sum(w => w.Value),
                    RecentChains = _recent,
                    PlayerLevel = _level,
                    OrderId = ++_id,
                    CompletedOrders = _id,
                    OpenDemand = id => (int)Math.Ceiling(openTaps[Index(id)] * 1.24),
                    ProducibleNow = id => charges[Index(id)],
                }, _random);
                int value = order.Requirements.Sum(r => OrderRules.Value(TestContent.Catalog.GetItem(r.ItemId)));
                LastLifebloom = order.Lifebloom;
                _recent.Add(order.ChainId);
                if (_recent.Count > OrderRules.MaxConsecutiveSameChain) _recent.RemoveAt(0);
                _window.Add((order.ChainId, value));
                if (_window.Count > OrderRules.DemandWindow) _window.RemoveAt(0);
                // A Lv1 generator yields 1.24 level-1 equivalents per tap (GDD §4.2).
                int taps = (int)Math.Ceiling(value / 1.24);
                return (order.ChainId == PrototypeContent.NatureChain ? 0 : 1, Math.Max(1, taps));
            }
        }

        /// <summary>Roadmap Phase 4 acceptance: players who follow their orders never stall with the real order generator.</summary>
        [Test]
        public void RealOrderGeneratorCausesNoStallsForPlayersWhoFollowTheirOrders(
            [Values("Light", "Core", "Engaged")] string profile,
            [Values(1, 3, 6, 10)] int playerLevel)
        {
            for (uint seed = 1; seed <= 50; seed++)
            {
                int comfort = 2 + (int)(seed % 4);
                var t = EnergySimulator.Run(EnergySimulator.Profiles[profile], new RealOrders(seed, playerLevel, comfort), "order-driven", new[] { 60, 60 }, 4);
                Assert.AreEqual(0, t.Stalls, "seed " + seed + " comfort " + comfort);
            }
        }

        /// <summary>
        /// Sensitivity (GDD §4.4): a "strict" player who only works on the oldest order and stops when its
        /// generator is empty, although other orders could still be worked on. Those stops are a choice, not
        /// forced by orders, so they are measured and capped rather than required to be zero (roadmap D38).
        /// </summary>
        [Test]
        public void StrictPlayersRarelyStopEarly()
        {
            int sessions = 0, stalls = 0;
            foreach (var profile in new[] { "Light", "Core", "Engaged" })
                foreach (int level in new[] { 1, 3, 6, 10 })
                    for (uint seed = 1; seed <= 50; seed++)
                    {
                        var t = EnergySimulator.Run(EnergySimulator.Profiles[profile], new RealOrders(seed, level, 2 + (int)(seed % 4)), "strict-order", new[] { 60, 60 }, 4);
                        sessions += EnergySimulator.Profiles[profile].Length * EnergySimulator.Days;
                        stalls += t.Stalls;
                    }
            double rate = stalls / (double)sessions;
            TestContext.WriteLine($"Strict players stop early in {stalls} of {sessions} sessions ({rate:P2})");
            Assert.LessOrEqual(rate, 0.02);
        }

        /// <summary>
        /// GDD §4.3 cooldown-skip measurement (roadmap D22): extra taps enabled by merge-skipped refresh waits
        /// must stay at or below 5% of a core player's daily taps. Generator sprouts currently come only from
        /// the Daily Gift (2 per 7 days). Each sprout allows at most one merge, and a merge can skip at most
        /// one refresh wait, which is worth at most one generator capacity of extra taps.
        /// </summary>
        [Test]
        public void MergeSkippedRefreshWaitsStayUnderFivePercentOfTaps()
        {
            int sproutsPerWeek = 0;
            for (int day = 0; day < Core.Economy.DailyGift.Days; day++)
                foreach (var r in Core.Economy.DailyGift.RewardsFor(day))
                    if (r.Kind == Core.Economy.RewardKind.Item && TestContent.Catalog.GetItem(r.ItemId).IsGenerator
                        && !TestContent.Catalog.GetItem(r.ItemId).Generator.IsConsumable) sproutsPerWeek++;

            int maxExtraTaps = sproutsPerWeek * TestContent.Catalog.GetItem(PrototypeContent.SeedPouch2).Generator.Capacity;
            var core = EnergySimulator.Run(EnergySimulator.Profiles["Core"], new EnergySimulator.PatternSource("capped-55"), "order-driven", new[] { 60, 60 }, 4);
            double share = maxExtraTaps / (double)core.Spent;
            TestContext.WriteLine($"Upper bound: {maxExtraTaps} extra taps per week vs {core.Spent} taps = {share:P1}");
            Assert.LessOrEqual(share, 0.05);
        }

        /// <summary>
        /// GDD §5.1 "days per area" (roadmap D39, D40): area costs divided by the Lifebloom a core player
        /// earns per day with real orders, at the player levels and comfort levels typical of each area.
        /// Targets: Area 1 in about 2–3 days, Area 2 in about 6 more days (8 cumulative).
        /// </summary>
        [TestCase(0, 2, 2, 1.0, 3.0)]
        [TestCase(0, 3, 2, 1.5, 3.0)]
        [TestCase(1, 6, 3, 4.5, 8.0)]
        [TestCase(1, 8, 4, 4.5, 8.0)]
        public void DaysPerAreaMeetTheTargets(int areaIndex, int playerLevel, int comfort, double minDays, double maxDays)
        {
            var area = Core.Content.StoryContent.Build().Areas[areaIndex];
            long lifebloom = 0;
            const int seeds = 30;
            for (uint seed = 1; seed <= seeds; seed++)
                lifebloom += EnergySimulator.Run(EnergySimulator.Profiles["Core"], new RealOrders(seed, playerLevel, comfort), "order-driven", new[] { 60, 60 }, 4).Lifebloom;
            double perDay = lifebloom / (double)seeds / EnergySimulator.Days;
            double days = area.TotalCost / perDay;
            TestContext.WriteLine($"{area.Name}: {area.TotalCost} Lifebloom at {perDay:0} per day = {days:0.0} days (level {playerLevel}, comfort {comfort})");
            Assert.That(days, Is.InRange(minDays, maxDays));
        }
    }
}
