using System.Collections.Generic;
using NUnit.Framework;
using WorldTree.Core;
using WorldTree.Core.Common;
using WorldTree.Core.Content;
using WorldTree.Core.Items;
using WorldTree.Tests.Support;
using static WorldTree.Tests.Support.TestContent;

namespace WorldTree.Tests
{
    /// <summary>GDD §4.1: tap checks, refresh cycle, placement, drops.</summary>
    public class GeneratorTests
    {
        private FakeClock _clock;
        private GameSession _s;
        private static readonly GridPos Gen = new GridPos(3, 4);

        [SetUp]
        public void SetUp()
        {
            _clock = FakeClock.AtNoon();
            _s = EmptySession(_clock);
            Put(_s, Gen.X, Gen.Y, PrototypeContent.SeedPouch1);
        }

        [Test]
        public void TapSpendsOneSapAndOneChargeAndSpawnsANatureItem()
        {
            var r = _s.TapCell(Gen);
            Assert.AreEqual(TapResult.Spawned, r.Result);
            Assert.AreEqual(99, _s.Sap);
            Assert.AreEqual(59, _s.GetGeneratorState(Gen).Charges);
            StringAssert.StartsWith("item.nature.", _s.Board.GetItem(r.SpawnPos).ItemId);
            Assert.AreEqual(1, _s.Stats.Taps);
            Assert.AreEqual(1, _s.Stats.SapSpent);
        }

        [Test]
        public void SpawnUsesNearestEmptyOpenCellWithRowThenColumnTieBreak()
        {
            // Distance-1 neighbours of (3,4): (3,3) is the smallest row, so it wins.
            Assert.AreEqual(P(3, 3), _s.TapCell(Gen).SpawnPos);
            // Next: (2,4) and (4,4) are both distance 1 on row 4; smaller column wins.
            Assert.AreEqual(P(2, 4), _s.TapCell(Gen).SpawnPos);
            Assert.AreEqual(P(4, 4), _s.TapCell(Gen).SpawnPos);
            Assert.AreEqual(P(3, 5), _s.TapCell(Gen).SpawnPos);
            // Distance 2, smallest row first: (3,2).
            Assert.AreEqual(P(3, 2), _s.TapCell(Gen).SpawnPos);
        }

        [Test]
        public void BoardFullSpendsNothing()
        {
            FillBoard(_s, "item.water.06");
            long v = _s.StateVersion;
            Assert.AreEqual(TapResult.BoardFull, _s.TapCell(Gen).Result);
            Assert.AreEqual(100, _s.Sap);
            Assert.AreEqual(60, _s.GetGeneratorState(Gen).Charges);
            Assert.IsNull(_s.GetGeneratorState(Gen).RefreshEndsUtc);
            Assert.AreEqual(v, _s.StateVersion);
        }

        [Test]
        public void NotEnoughSapSpendsNoCharge()
        {
            Assert.IsTrue(_s.Energy.TrySpend(100, _clock.UtcNow));
            Assert.AreEqual(TapResult.NotEnoughSap, _s.TapCell(Gen).Result);
            Assert.AreEqual(60, _s.GetGeneratorState(Gen).Charges);
            Assert.IsNull(_s.GetGeneratorState(Gen).RefreshEndsUtc);
        }

        [Test]
        public void NoChargesSpendsNoSap()
        {
            for (int i = 0; i < 60; i++) Assert.AreEqual(TapResult.Spawned, _s.TapCell(Gen).Result, "tap " + i);
            _s.DebugAddSap(50);
            int sap = _s.Sap;
            Assert.AreEqual(TapResult.NoCharges, _s.TapCell(Gen).Result);
            Assert.AreEqual(sap, _s.Sap);
        }

        [Test]
        public void TappingNonGeneratorOrEmptyOrLockedCellDoesNothing()
        {
            Put(_s, 0, 0, "item.nature.01");
            Assert.AreEqual(TapResult.NotAGenerator, _s.TapCell(P(0, 0)).Result);
            Assert.AreEqual(TapResult.Empty, _s.TapCell(P(6, 8)).Result);
            Assert.AreEqual(TapResult.OutOfBounds, _s.TapCell(P(7, 0)).Result);
            _s.Board.SetState(Gen, Core.Board.CellState.Withered);
            Assert.AreEqual(TapResult.Locked, _s.TapCell(Gen).Result);
        }

        [Test]
        public void FirstTapOnFullGeneratorStartsTimerAndLaterTapsDoNotRestartIt()
        {
            var start = _clock.UtcNow;
            _s.TapCell(Gen);
            Assert.AreEqual(start + Minutes(20), _s.GetGeneratorState(Gen).RefreshEndsUtc);
            _clock.AdvanceMinutes(5);
            _s.TapCell(Gen);
            Assert.AreEqual(start + Minutes(20), _s.GetGeneratorState(Gen).RefreshEndsUtc);
        }

        [Test]
        public void AtRefreshEndGeneratorIsFullWhateverIsLeft()
        {
            for (int i = 0; i < 10; i++) _s.TapCell(Gen);
            Assert.AreEqual(50, _s.GetGeneratorState(Gen).Charges);
            _clock.Advance(Minutes(20) - System.TimeSpan.FromSeconds(1));
            Assert.AreEqual(50, _s.GetGeneratorState(Gen).Charges);
            _clock.AdvanceSeconds(1);
            var state = _s.GetGeneratorState(Gen);
            Assert.AreEqual(60, state.Charges);
            Assert.IsNull(state.RefreshEndsUtc);
        }

        [Test]
        public void DepletedGeneratorWaitsForCycleEndThenIsFull()
        {
            _s.DebugAddSap(100);
            for (int i = 0; i < 60; i++) _s.TapCell(Gen);
            Assert.AreEqual(TapResult.NoCharges, _s.TapCell(Gen).Result);
            _clock.AdvanceMinutes(20);
            Assert.AreEqual(TapResult.Spawned, _s.TapCell(Gen).Result);
            Assert.AreEqual(59, _s.GetGeneratorState(Gen).Charges);
        }

        [Test]
        public void RefreshCompletesWhileOffline()
        {
            _s.TapCell(Gen);
            _clock.AdvanceMinutes(60 * 7);
            Assert.AreEqual(60, _s.GetGeneratorState(Gen).Charges);
        }

        [Test]
        public void ClockMovedBackClampsRemainingTimeToOneCycle()
        {
            _s.TapCell(Gen);
            _clock.AdvanceMinutes(-120);
            var state = _s.GetGeneratorState(Gen);
            Assert.AreEqual(_clock.UtcNow + Minutes(20), state.RefreshEndsUtc);
            _clock.AdvanceMinutes(20);
            Assert.AreEqual(60, _s.GetGeneratorState(Gen).Charges);
        }

        [Test]
        public void GeneratorStateFollowsTheItemWhenMovedAndSwapped()
        {
            for (int i = 0; i < 3; i++) _s.TapCell(Gen);
            var state = _s.GetGeneratorState(Gen);
            _s.DragItem(Gen, P(0, 8));
            Assert.AreSame(state, _s.GetGeneratorState(P(0, 8)));
            Put(_s, 6, 0, "item.water.01");
            _s.DragItem(P(0, 8), P(6, 0));
            Assert.AreSame(state, _s.GetGeneratorState(P(6, 0)));
            Assert.AreEqual(57, state.Charges);
        }

        [Test]
        public void DropTableRespectsWeights()
        {
            var table = TestContent.Catalog.GetItem(PrototypeContent.SeedPouch1).Generator.Drops;
            var random = new XorShiftRandom(42);
            var counts = new Dictionary<string, int>();
            const int rolls = 100000;
            for (int i = 0; i < rolls; i++)
            {
                string id = table.Roll(random);
                counts[id] = counts.TryGetValue(id, out var c) ? c + 1 : 1;
            }
            Assert.AreEqual(0.80, counts["item.nature.01"] / (double)rolls, 0.01);
            Assert.AreEqual(0.18, counts["item.nature.02"] / (double)rolls, 0.01);
            Assert.AreEqual(0.02, counts["item.nature.03"] / (double)rolls, 0.005);
            Assert.AreEqual(3, counts.Count);
        }

        [Test]
        public void SameSeedProducesSameDrops()
        {
            var a = EmptySession(FakeClock.AtNoon(), seed: 7);
            var b = EmptySession(FakeClock.AtNoon(), seed: 7);
            Put(a, 3, 4, PrototypeContent.SeedPouch1);
            Put(b, 3, 4, PrototypeContent.SeedPouch1);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(a.TapCell(Gen).SpawnedItemId, b.TapCell(Gen).SpawnedItemId);
        }
    }
}
