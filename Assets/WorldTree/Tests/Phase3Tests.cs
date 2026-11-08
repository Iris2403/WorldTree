using System;
using System.Collections.Generic;
using NUnit.Framework;
using WorldTree.Core;
using WorldTree.Core.Board;
using WorldTree.Core.Content;
using WorldTree.Core.Economy;
using WorldTree.Core.Save;
using WorldTree.Tests.Support;
using static WorldTree.Tests.Support.TestContent;

namespace WorldTree.Tests
{
    /// <summary>Phase 3: consumable generators, refresh skip, ad speed-up, Sap refills, ad Sap, Daily Gift.</summary>
    public class Phase3Tests
    {
        private FakeClock _clock;
        private GameSession _s;
        private static readonly Core.Common.GridPos Gen = new Core.Common.GridPos(3, 4);

        [SetUp]
        public void SetUp()
        {
            _clock = FakeClock.AtNoon();
            _s = EmptySession(_clock);
        }

        // ---------- Consumable generators ----------

        [Test]
        public void MysteryAcornGivesFixedTapsThenVanishes()
        {
            Put(_s, Gen.X, Gen.Y, PrototypeContent.MysteryAcorn);
            bool exhausted = false;
            _s.GeneratorExhausted += p => exhausted = p == Gen;
            for (int i = 0; i < PrototypeContent.MysteryAcornTaps; i++)
                Assert.AreEqual(TapResult.Spawned, _s.TapCell(Gen).Result, "tap " + i);
            Assert.IsTrue(exhausted);
            Assert.IsNull(_s.Board.GetItem(Gen));
            Assert.AreEqual(100 - PrototypeContent.MysteryAcornTaps, _s.Sap);
        }

        [Test]
        public void MysteryAcornNeverRefreshes()
        {
            Put(_s, Gen.X, Gen.Y, PrototypeContent.MysteryAcorn);
            _s.TapCell(Gen);
            Assert.IsNull(_s.GetGeneratorState(Gen).RefreshEndsUtc);
            _clock.AdvanceMinutes(600);
            _s.DebugRefreshAllGenerators();
            Assert.AreEqual(PrototypeContent.MysteryAcornTaps - 1, _s.GetGeneratorState(Gen).Charges);
            Assert.AreEqual(SkipResult.NotAGenerator, _s.SkipRefresh(Gen));
        }

        [Test]
        public void MysteryAcornCanBeSoldButPermanentGeneratorsCannot()
        {
            Put(_s, 0, 0, PrototypeContent.MysteryAcorn);
            Put(_s, 1, 0, PrototypeContent.SeedPouch1);
            Assert.AreEqual(SellResult.Sold, _s.SellItem(P(0, 0)));
            Assert.AreEqual(SellResult.NotSellable, _s.SellItem(P(1, 0)));
        }

        [Test]
        public void PartlyUsedAcornSurvivesSaveAndLoad()
        {
            Put(_s, Gen.X, Gen.Y, PrototypeContent.MysteryAcorn);
            _s.TapCell(Gen);
            _s.TapCell(Gen);
            var restored = SaveMapper.Restore(SaveMapper.Capture(_s), TestContent.Catalog, PrototypeContent.Energy, _clock, new List<string>());
            Assert.AreEqual(PrototypeContent.MysteryAcornTaps - 2, restored.GetGeneratorState(Gen).Charges);
            Assert.IsNull(restored.GetGeneratorState(Gen).RefreshEndsUtc);
        }

        // ---------- Refresh skip ----------

        [Test]
        public void SkipCostsOneMoonpetalPerStartedFiveMinutes()
        {
            Assert.AreEqual(4, EconomyRules.SkipCost(TimeSpan.FromMinutes(20)));
            Assert.AreEqual(3, EconomyRules.SkipCost(TimeSpan.FromMinutes(14)));
            Assert.AreEqual(1, EconomyRules.SkipCost(TimeSpan.FromSeconds(10)));
            Assert.AreEqual(0, EconomyRules.SkipCost(TimeSpan.Zero));
        }

        [Test]
        public void SkipMakesGeneratorFullAndSpendsMoonpetals()
        {
            Put(_s, Gen.X, Gen.Y, PrototypeContent.SeedPouch1);
            for (int i = 0; i < 5; i++) _s.TapCell(Gen);
            _s.DebugAddMoonpetals(10);
            Assert.AreEqual(4, _s.SkipCostAt(Gen));
            Assert.AreEqual(SkipResult.Skipped, _s.SkipRefresh(Gen));
            Assert.AreEqual(60, _s.GetGeneratorState(Gen).Charges);
            Assert.IsNull(_s.GetGeneratorState(Gen).RefreshEndsUtc);
            Assert.AreEqual(6, _s.Wallet.Moonpetals);
        }

        [Test]
        public void SkipFailsAtomicallyWithoutEnoughMoonpetalsOrWhenFull()
        {
            Put(_s, Gen.X, Gen.Y, PrototypeContent.SeedPouch1);
            Assert.AreEqual(SkipResult.NothingToSkip, _s.SkipRefresh(Gen));
            _s.TapCell(Gen);
            _s.DebugAddMoonpetals(3);
            Assert.AreEqual(SkipResult.NotEnoughMoonpetals, _s.SkipRefresh(Gen));
            Assert.AreEqual(59, _s.GetGeneratorState(Gen).Charges);
            Assert.AreEqual(3, _s.Wallet.Moonpetals);
        }

        // ---------- Ad speed-up ----------

        [Test]
        public void AdSpeedUpHalvesRemainingTimeOncePerCycle()
        {
            Put(_s, Gen.X, Gen.Y, PrototypeContent.SeedPouch1);
            _s.TapCell(Gen);
            _clock.AdvanceMinutes(4); // 16 min left
            Assert.AreEqual(PurchaseResult.Done, _s.ApplyAdSpeedUp(Gen));
            Assert.AreEqual(_clock.UtcNow + Minutes(8), _s.GetGeneratorState(Gen).RefreshEndsUtc);
            Assert.AreEqual(PurchaseResult.NotApplicable, _s.ApplyAdSpeedUp(Gen), "once per cycle");

            _clock.AdvanceMinutes(8);
            Assert.AreEqual(60, _s.GetGeneratorState(Gen).Charges);
            _s.TapCell(Gen);
            Assert.AreEqual(PurchaseResult.Done, _s.ApplyAdSpeedUp(Gen), "a new cycle allows it again");
        }

        [Test]
        public void AdSpeedUpNeedsARunningCycle()
        {
            Put(_s, Gen.X, Gen.Y, PrototypeContent.SeedPouch1);
            Assert.AreEqual(PurchaseResult.NotApplicable, _s.ApplyAdSpeedUp(Gen));
        }

        // ---------- Sap refill and ad Sap ----------

        [Test]
        public void SapRefillAddsHundredAndCostDoublesWithinTheDay()
        {
            _s.DebugAddMoonpetals(100);
            Assert.AreEqual(10, _s.SapRefillCost);
            Assert.AreEqual(PurchaseResult.Done, _s.RefillSap());
            Assert.AreEqual(200, _s.Sap, "over the cap is allowed");
            Assert.AreEqual(20, _s.SapRefillCost);
            _s.RefillSap();
            Assert.AreEqual(40, _s.SapRefillCost);
            Assert.AreEqual(70, _s.Wallet.Moonpetals);
        }

        [Test]
        public void SapRefillCostResetsAtLocalMidnight()
        {
            _clock.UtcNow = new DateTime(2026, 1, 1, 21, 0, 0, DateTimeKind.Utc);
            _clock.LocalUtcOffset = TimeSpan.FromHours(2); // local 23:00
            _s.DebugAddMoonpetals(100);
            _s.RefillSap();
            Assert.AreEqual(20, _s.SapRefillCost);
            _clock.AdvanceMinutes(59);
            Assert.AreEqual(20, _s.SapRefillCost, "still 23:59 local");
            _clock.AdvanceMinutes(2);
            Assert.AreEqual(10, _s.SapRefillCost, "local midnight passed");
        }

        [Test]
        public void SapRefillFailsAtomicallyWithoutMoonpetals()
        {
            Assert.AreEqual(PurchaseResult.NotEnoughMoonpetals, _s.RefillSap());
            Assert.AreEqual(100, _s.Sap);
            Assert.AreEqual(10, _s.SapRefillCost);
        }

        [Test]
        public void AdSapIsCappedAtFivePerDayAndResets()
        {
            for (int i = 0; i < EconomyRules.AdSapDailyCap; i++) Assert.AreEqual(PurchaseResult.Done, _s.GrantAdSap());
            Assert.AreEqual(PurchaseResult.DailyLimitReached, _s.GrantAdSap());
            Assert.AreEqual(100 + 5 * EconomyRules.AdSapAmount, _s.Sap);
            _clock.AdvanceMinutes(24 * 60);
            Assert.AreEqual(EconomyRules.AdSapDailyCap, _s.AdSapLeftToday);
        }

        // ---------- Daily Gift ----------

        [Test]
        public void DailyGiftCanBeClaimedOncePerDay()
        {
            Assert.IsTrue(_s.CanClaimDailyGift);
            Assert.AreEqual(ClaimResult.Claimed, _s.ClaimDailyGift());
            Assert.AreEqual(130, _s.Sap, "day 1: +30 Sap");
            Assert.IsFalse(_s.CanClaimDailyGift);
            Assert.AreEqual(ClaimResult.AlreadyClaimedToday, _s.ClaimDailyGift());
            _clock.AdvanceMinutes(24 * 60);
            Assert.AreEqual(ClaimResult.Claimed, _s.ClaimDailyGift());
            Assert.AreEqual(25, _s.Wallet.Glimmer, "day 2: +25 Glimmer");
        }

        [Test]
        public void MissingDaysPausesTheCalendarInsteadOfResetting()
        {
            _s.ClaimDailyGift();
            _clock.AdvanceMinutes(24 * 60 * 5);
            Assert.AreEqual(1, _s.DailyGift.NextDayIndex);
            _s.ClaimDailyGift();
            Assert.AreEqual(25, _s.Wallet.Glimmer, "day 2's reward, not day 6's");
        }

        [Test]
        public void ItemRewardsGoToTheBoardThenTheSatchel()
        {
            ClaimDays(2);
            Assert.AreEqual(ClaimResult.Claimed, ClaimNextDay()); // day 3: a Seed Pouch sprout
            Assert.AreEqual(PrototypeContent.SeedPouch1, _s.Board.GetItem(P(3, 8)).ItemId);

            FillBoard(_s, "item.nature.08");
            ClaimNextDay(); // day 4: Sap
            Assert.AreEqual(ClaimResult.Claimed, ClaimNextDay()); // day 5: Mystery Acorn, board full
            Assert.AreEqual(PrototypeContent.MysteryAcorn, _s.Satchel.Items[0].ItemId);
        }

        [Test]
        public void ItemRewardWithNoRoomAnywhereGrantsNothing()
        {
            ClaimDays(2); // the next gift (day 3) is an item
            for (int i = 0; i < 4; i++)
            {
                Put(_s, i, 0, "item.water.01");
                Assert.AreEqual(StoreResult.Stored, _s.StoreInSatchel(P(i, 0)));
            }
            FillBoard(_s, "item.nature.08");
            Assert.AreEqual(ClaimResult.NoRoom, _s.ClaimDailyGift());
            Assert.AreEqual(2, _s.DailyGift.NextDayIndex, "not advanced");
            Assert.IsTrue(_s.CanClaimDailyGift, "can retry after making room");
        }

        [Test]
        public void DaySevenGivesMoonpetalsAndTheCalendarLoops()
        {
            ClaimDays(6);
            int sapBefore = _s.Sap;
            Assert.AreEqual(ClaimResult.Claimed, ClaimNextDay());
            Assert.AreEqual(20, _s.Wallet.Moonpetals);
            Assert.AreEqual(sapBefore + 50, _s.Sap);
            Assert.AreEqual(0, _s.DailyGift.NextDayIndex, "back to day 1");
        }

        [Test]
        public void Phase3StateRoundTrips()
        {
            Put(_s, Gen.X, Gen.Y, PrototypeContent.SeedPouch1);
            _s.TapCell(Gen);
            _s.ApplyAdSpeedUp(Gen);
            _s.DebugAddMoonpetals(50);
            _s.RefillSap();
            _s.GrantAdSap();
            _s.ClaimDailyGift();

            var fs = new InMemoryFileSystem();
            new AtomicSaveStore(fs, "saves").Save(SaveMapper.Capture(_s), _clock.UtcNow, false);
            var data = new AtomicSaveStore(fs, "saves").Load(_clock.UtcNow).Data;
            var r = SaveMapper.Restore(data, TestContent.Catalog, PrototypeContent.Energy, _clock, new List<string>());

            Assert.AreEqual(40, r.Wallet.Moonpetals);
            Assert.AreEqual(20, r.SapRefillCost);
            Assert.AreEqual(EconomyRules.AdSapDailyCap - 1, r.AdSapLeftToday);
            Assert.IsFalse(r.CanClaimDailyGift);
            Assert.AreEqual(1, r.DailyGift.NextDayIndex);
            Assert.IsTrue(r.GetGeneratorState(Gen).AdSpeedUpUsed);
            Assert.AreEqual(PurchaseResult.NotApplicable, r.ApplyAdSpeedUp(Gen));
        }

        private void ClaimDays(int days)
        {
            for (int i = 0; i < days; i++) Assert.AreEqual(ClaimResult.Claimed, ClaimNextDay(), "day " + (i + 1));
        }

        private ClaimResult ClaimNextDay()
        {
            var result = _s.ClaimDailyGift();
            _clock.AdvanceMinutes(24 * 60);
            return result;
        }
    }
}
