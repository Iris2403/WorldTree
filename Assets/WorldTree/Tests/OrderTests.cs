using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTree.Core;
using WorldTree.Core.Board;
using WorldTree.Core.Common;
using WorldTree.Core.Content;
using WorldTree.Core.Items;
using WorldTree.Core.Orders;
using WorldTree.Core.Progression;
using WorldTree.Core.Save;
using WorldTree.Tests.Support;
using static WorldTree.Tests.Support.TestContent;

namespace WorldTree.Tests
{
    /// <summary>GDD §6 orders, §7.1 levels: generation, delivery, rewards, swapping, level-ups.</summary>
    public class OrderTests
    {
        private FakeClock _clock;

        [SetUp]
        public void SetUp() => _clock = FakeClock.AtNoon();

        private GameSession NewGame(uint seed = 7) =>
            GameSession.NewGame(TestContent.Catalog, PrototypeContent.StartingLayout(), PrototypeContent.Energy, _clock, seed);

        /// <summary>
        /// Session with the given generators on the board. They must be placed before the session is
        /// created, because the first orders are generated from the generators available at that moment.
        /// </summary>
        private GameSession SessionWith(params string[] generators)
        {
            var board = new BoardModel(7, 9);
            for (int i = 0; i < generators.Length; i++) board.SetItem(P(i, 8), TestContent.Item(generators[i]));
            return new GameSession(TestContent.Catalog, board, Core.Energy.EnergyModel.Full(Core.Energy.EnergyConfig.Default), 12345, _clock);
        }

        private static void PutRequired(GameSession s, Order order)
        {
            int x = 0;
            foreach (var r in order.Requirements)
                if (!r.Fulfilled) Put(s, x++, 0, r.ItemId);
        }

        // ---------- Generation ----------

        [Test]
        public void NewGameStartsWithThreeSmallNatureOrders()
        {
            var s = NewGame();
            Assert.AreEqual(3, s.Orders.Active.Count);
            foreach (var o in s.Orders.Active)
            {
                Assert.AreEqual(PrototypeContent.NatureChain, o.ChainId, "only the Seed Pouch is available");
                Assert.AreEqual(1, o.Requirements.Count, "level 1 orders ask for one item");
            }
        }

        [Test]
        public void OrdersOnlyRequestChainsWithAGeneratorOnTheBoardOrInTheSatchel()
        {
            var s = SessionWith(PrototypeContent.Moonwell1);
            CollectionAssert.AreEquivalent(new[] { PrototypeContent.WaterChain }, s.AvailableItemChains().Select(c => c.Id));
            s.StoreInSatchel(P(0, 8));
            CollectionAssert.AreEquivalent(new[] { PrototypeContent.WaterChain }, s.AvailableItemChains().Select(c => c.Id), "satchel counts");
            Put(s, 3, 3, PrototypeContent.MysteryAcorn);
            Assert.AreEqual(1, s.AvailableItemChains().Count, "consumables don't make a chain available");
            Put(s, 4, 4, PrototypeContent.Stump1);
            s.Board.SetState(P(4, 4), CellState.Withered);
            Assert.AreEqual(1, s.AvailableItemChains().Count, "locked generators don't count");
        }

        [Test]
        public void NoGeneratorMeansNoOrders()
        {
            var s = EmptySession(_clock);
            Assert.AreEqual(0, s.Orders.Active.Count);
        }

        /// <summary>
        /// Property test over thousands of seeded runs (roadmap Phase 4 acceptance): orders are always
        /// feasible, never more than 2 in a row from one chain when another is available, and over the
        /// order stream no chain exceeds 55% of demand.
        /// </summary>
        [Test]
        public void GeneratedOrderStreamsRespectFeasibilityAndTheDemandRule()
        {
            var all = TestContent.Catalog.Chains.Where(c => c.Id == PrototypeContent.NatureChain || c.Id == PrototypeContent.WaterChain || c.Id == PrototypeContent.CraftChain).ToList();
            for (uint seed = 1; seed <= 3000; seed++)
            {
                var random = new XorShiftRandom(seed);
                int chainCount = 1 + (int)(seed % 3);
                var available = all.Take(chainCount).ToList();
                int level = 1 + (int)(seed % 12);
                int comfort = 1 + (int)(seed % 6);
                var active = new List<Order>();
                var recent = new List<string>();
                var window = new List<(string chain, int value)>();
                var demand = new Dictionary<string, int>();
                int completed = 0;

                for (int n = 0; n < 60; n++)
                {
                    while (active.Count < 3)
                    {
                        var order = OrderGenerator.Generate(new OrderGenerator.Context
                        {
                            AvailableChains = available,
                            ComfortLevel = _ => comfort,
                            RecentDemand = id => window.Where(w => w.chain == id).Sum(w => w.value),
                            RecentTotal = window.Sum(w => w.value),
                            RecentChains = recent,
                            PlayerLevel = level,
                            OrderId = n + 1,
                            CompletedOrders = completed,
                        }, random);
                        Assert.IsTrue(available.Any(c => c.Id == order.ChainId), "feasible");
                        foreach (var r in order.Requirements)
                            Assert.AreEqual(order.ChainId, TestContent.Catalog.GetItem(r.ItemId).ChainId);
                        Assert.That(order.Requirements.Count, Is.InRange(1, OrderRules.MaxItemsForLevel(level)));
                        if (available.Count > 1 && recent.Count >= 2)
                            Assert.IsFalse(recent[recent.Count - 1] == order.ChainId && recent[recent.Count - 2] == order.ChainId, "max 2 in a row, seed " + seed);
                        recent.Add(order.ChainId);
                        if (recent.Count > 2) recent.RemoveAt(0);
                        int value = order.Requirements.Sum(r => OrderRules.Value(TestContent.Catalog.GetItem(r.ItemId)));
                        demand[order.ChainId] = (demand.TryGetValue(order.ChainId, out var d) ? d : 0) + value;
                        window.Add((order.ChainId, value));
                        if (window.Count > OrderRules.DemandWindow) window.RemoveAt(0);
                        active.Add(order);
                    }
                    active.RemoveAt(0); // the player completes the oldest order
                    completed++;
                }

                if (available.Count >= 2)
                {
                    double total = demand.Values.Sum();
                    foreach (var pair in demand)
                        Assert.LessOrEqual(pair.Value / total, OrderRules.MaxChainShare, "chain " + pair.Key + " seed " + seed);
                }
            }
        }

        [Test]
        public void RewardsScaleWithLevelOneEquivalents()
        {
            Assert.AreEqual(1, OrderRules.Value(TestContent.Catalog.GetItem("item.nature.01")));
            Assert.AreEqual(8, OrderRules.Value(TestContent.Catalog.GetItem("item.nature.04")));
            Assert.AreEqual(1, OrderRules.LifebloomFor(1));
            Assert.AreEqual(2, OrderRules.LifebloomFor(8));
            Assert.AreEqual(4, OrderRules.LifebloomFor(16));
        }

        // ---------- Delivery ----------

        [Test]
        public void DeliverCompletesOrderRemovesItemsAndGrantsRewards()
        {
            var s = SessionWith(PrototypeContent.SeedPouch1);
            var order = s.Orders.Active[0];
            Assert.IsFalse(s.CanDeliver(order.Id));
            Assert.AreEqual(DeliverResult.MissingItems, s.DeliverOrder(order.Id));
            PutRequired(s, order);
            Assert.IsTrue(s.CanDeliver(order.Id));
            int glimmer = s.Wallet.Glimmer;
            Order completed = null;
            s.OrderCompleted += o => completed = o;

            Assert.AreEqual(DeliverResult.Completed, s.DeliverOrder(order.Id));
            Assert.AreSame(order, completed);
            Assert.IsNull(s.Board.GetItem(P(0, 0)));
            Assert.AreEqual(glimmer + order.Glimmer, s.Wallet.Glimmer);
            Assert.AreEqual(order.Lifebloom, s.Wallet.Lifebloom);
            Assert.IsNull(s.Orders.Find(order.Id));
            Assert.AreEqual(3, s.Orders.Active.Count, "a new order replaces it");
            Assert.AreEqual(1, s.Orders.Completed);
        }

        [Test]
        public void LockedItemsDoNotCountForDelivery()
        {
            var s = SessionWith(PrototypeContent.SeedPouch1);
            var order = s.Orders.Active[0];
            PutRequired(s, order);
            s.Board.SetState(P(0, 0), CellState.Withered);
            Assert.IsFalse(s.CanDeliver(order.Id));
        }

        [Test]
        public void DraggingItemsOntoTheOrderFulfilsItPieceByPiece()
        {
            _clock = FakeClock.AtNoon();
            var s = SessionWith(PrototypeContent.SeedPouch1);
            // Reach level 6 so orders can ask for several items, then pick an order that does.
            DeliverMany(s, 25);
            var order = s.Orders.Active.FirstOrDefault(o => o.Requirements.Count >= 2);
            Assume.That(order, Is.Not.Null, "needs a multi-item order");

            Put(s, 6, 6, "item.water.08");
            Assert.AreEqual(DeliverResult.NotNeeded, s.ContributeToOrder(P(6, 6), order.Id));
            Put(s, 0, 0, order.Requirements[0].ItemId);
            Assert.AreEqual(DeliverResult.Accepted, s.ContributeToOrder(P(0, 0), order.Id));
            Assert.IsNull(s.Board.GetItem(P(0, 0)));
            Assert.IsTrue(order.HasProgress);
            for (int i = 1; i < order.Requirements.Count; i++)
            {
                Put(s, i, 0, order.Requirements[i].ItemId);
                var expected = i == order.Requirements.Count - 1 ? DeliverResult.Completed : DeliverResult.Accepted;
                Assert.AreEqual(expected, s.ContributeToOrder(P(i, 0), order.Id));
            }
        }

        [Test]
        public void EveryFifthOrderGivesABonusMysteryAcorn()
        {
            var s = SessionWith(PrototypeContent.SeedPouch1);
            int acorns = 0;
            for (int i = 0; i < 10; i++)
            {
                var order = s.Orders.Active[0];
                if (order.BonusItemId != null) acorns++;
                PutRequired(s, order);
                s.DeliverOrder(order.Id);
            }
            Assert.AreEqual(2, acorns);
        }

        // ---------- Swapping ----------

        [Test]
        public void SwapReplacesAnOrderOncePerThirtyMinutes()
        {
            var s = SessionWith(PrototypeContent.SeedPouch1);
            var first = s.Orders.Active[0];
            Assert.AreEqual(SwapOrderResult.Swapped, s.SwapOrder(first.Id));
            Assert.IsNull(s.Orders.Find(first.Id));
            Assert.AreEqual(3, s.Orders.Active.Count);
            Assert.AreEqual(SwapOrderResult.OnCooldown, s.SwapOrder(s.Orders.Active[0].Id));
            _clock.AdvanceMinutes(30);
            Assert.AreEqual(SwapOrderResult.Swapped, s.SwapOrder(s.Orders.Active[0].Id));
        }

        // ---------- Levels ----------

        [Test]
        public void XpLevelsUpAndEachLevelUpRefillsSapAndPays()
        {
            var s = SessionWith(PrototypeContent.SeedPouch1);
            s.Energy.TrySpend(90, _clock.UtcNow);
            var levels = new List<int>();
            s.LeveledUp += l => levels.Add(l);
            int moonpetals = s.Wallet.Moonpetals;
            DeliverMany(s, 30);
            Assert.That(s.Progress.Level, Is.GreaterThan(1));
            CollectionAssert.AreEqual(Enumerable.Range(2, s.Progress.Level - 1).ToList(), levels);
            Assert.GreaterOrEqual(s.Sap, 100, "level-up refills Sap to at least the cap (roadmap D33)");
            Assert.AreEqual(moonpetals + PlayerProgress.LevelUpMoonpetals * (s.Progress.Level - 1), s.Wallet.Moonpetals);
        }

        [Test]
        public void LevelUpNeverReducesOverCapSap()
        {
            var s = SessionWith(PrototypeContent.SeedPouch1);
            s.DebugAddSap(150);
            DeliverMany(s, 30);
            Assert.GreaterOrEqual(s.Sap, 250);
        }

        [Test]
        public void FourthOrderSlotUnlocksAtLevelEight()
        {
            var s = SessionWith(PrototypeContent.SeedPouch1);
            while (s.Progress.Level < OrderRules.FourthSlotLevel) DeliverMany(s, 1);
            Assert.AreEqual(4, s.Orders.Active.Count);
        }

        [Test]
        public void XpCurveIsFifteenTimesLevel()
        {
            Assert.AreEqual(15, PlayerProgress.XpRequired(1));
            Assert.AreEqual(105, PlayerProgress.XpRequired(7));
        }

        // ---------- Saving ----------

        [Test]
        public void OrdersProgressAndLevelSurviveSaveAndLoad()
        {
            var s = SessionWith(PrototypeContent.SeedPouch1);
            DeliverMany(s, 25);
            var order = s.Orders.Active.FirstOrDefault(o => o.Requirements.Count >= 2) ?? s.Orders.Active[0];
            Put(s, 0, 0, order.Requirements[0].ItemId);
            s.ContributeToOrder(P(0, 0), order.Id);
            s.SwapOrder(s.Orders.Active.First(o => !o.HasProgress).Id);

            var r = SaveMapper.Restore(SaveMapper.Capture(s), TestContent.Catalog, PrototypeContent.Energy, _clock, new List<string>());
            Assert.AreEqual(JsonOf(s), JsonOf(r));
            Assert.AreEqual(s.Progress.Level, r.Progress.Level);
            Assert.AreEqual(s.Wallet.Lifebloom, r.Wallet.Lifebloom);
            Assert.AreEqual(OrderRules.SwapCooldown - TimeSpan.Zero, r.OrderSwapCooldownLeft);
        }

        private static string JsonOf(GameSession s) => Newtonsoft.Json.JsonConvert.SerializeObject(SaveMapper.Capture(s));

        private static void DeliverMany(GameSession s, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var order = s.Orders.Active[0];
                PutRequired(s, order);
                Assert.AreEqual(DeliverResult.Completed, s.DeliverOrder(order.Id));
                // Keep the bonus acorns from filling the board.
                for (int y = 0; y < s.Board.Height; y++)
                    for (int x = 0; x < s.Board.Width; x++)
                    {
                        var item = s.Board.GetItem(new GridPos(x, y));
                        if (item != null && item.ItemId == PrototypeContent.MysteryAcorn) s.Board.SetItem(new GridPos(x, y), null);
                    }
            }
        }
    }
}
