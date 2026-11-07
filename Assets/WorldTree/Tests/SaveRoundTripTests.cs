using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using WorldTree.Core;
using WorldTree.Core.Content;
using WorldTree.Core.Save;
using WorldTree.Tests.Support;
using static WorldTree.Tests.Support.TestContent;

namespace WorldTree.Tests
{
    /// <summary>Full round trip GameSession → disk → GameSession, including RNG state and timers.</summary>
    public class SaveRoundTripTests
    {
        private static string Snapshot(GameSession s) => JsonConvert.SerializeObject(SaveMapper.Capture(s));

        private static GameSession Reload(GameSession s, FakeClock clock, List<string> warnings = null)
        {
            var fs = new InMemoryFileSystem();
            var store = new AtomicSaveStore(fs, "saves");
            store.Save(SaveMapper.Capture(s), clock.UtcNow, false);
            var loaded = new AtomicSaveStore(fs, "saves").Load(clock.UtcNow);
            return SaveMapper.Restore(loaded.Data, TestContent.Catalog, PrototypeContent.Energy, clock, warnings ?? new List<string>());
        }

        [Test]
        public void RoundTripPreservesBoardGeneratorsSapAndRandomSequence()
        {
            var clock = FakeClock.AtNoon();
            var s = GameSession.NewGame(TestContent.Catalog, PrototypeContent.StartingLayout(), PrototypeContent.Energy, clock, 2024);
            var seedPouch = P(2, 4);
            for (int i = 0; i < 12; i++) s.TapCell(seedPouch);
            s.DragItem(P(1, 1), P(5, 1));
            clock.AdvanceMinutes(3);

            var restored = Reload(s, clock);
            Assert.AreEqual(Snapshot(s), Snapshot(restored));
            Assert.AreEqual(s.Sap, restored.Sap);
            Assert.AreEqual(s.GetGeneratorState(seedPouch).RefreshEndsUtc, restored.GetGeneratorState(seedPouch).RefreshEndsUtc);

            // The random sequence continues identically after reloading.
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(s.TapCell(seedPouch).SpawnedItemId, restored.TapCell(seedPouch).SpawnedItemId);
        }

        [Test]
        public void TimersKeepRunningAcrossReload()
        {
            var clock = FakeClock.AtNoon();
            var s = EmptySession(clock);
            Put(s, 3, 4, PrototypeContent.SeedPouch1);
            for (int i = 0; i < 5; i++) s.TapCell(P(3, 4));
            var restored = Reload(s, clock);
            clock.AdvanceMinutes(20);
            Assert.AreEqual(60, restored.GetGeneratorState(P(3, 4)).Charges);
            Assert.AreEqual(100, restored.Sap);
        }

        [Test]
        public void UnknownItemIsSkippedWithWarning()
        {
            var clock = FakeClock.AtNoon();
            var data = new SaveData { Width = 7, Height = 9, Sap = 40 };
            data.Cells.Add(new CellData { X = 0, Y = 0, ItemId = "item.removed.01" });
            data.Cells.Add(new CellData { X = 1, Y = 0, ItemId = "item.water.02" });
            var warnings = new List<string>();
            var s = SaveMapper.Restore(data, TestContent.Catalog, PrototypeContent.Energy, clock, warnings);
            Assert.IsNull(s.Board.GetItem(P(0, 0)));
            Assert.AreEqual("item.water.02", s.Board.GetItem(P(1, 0)).ItemId);
            Assert.AreEqual(1, warnings.Count);
            Assert.AreEqual(40, s.Energy.Amount);
        }

        [Test]
        public void PartlyUsedGeneratorWithoutTimerGetsOne()
        {
            var clock = FakeClock.AtNoon();
            var data = new SaveData { Width = 7, Height = 9, Sap = 100 };
            data.Cells.Add(new CellData { X = 0, Y = 0, ItemId = PrototypeContent.SeedPouch1, Charges = 10 });
            var s = SaveMapper.Restore(data, TestContent.Catalog, PrototypeContent.Energy, clock, new List<string>());
            Assert.AreEqual(clock.UtcNow + Minutes(20), s.GetGeneratorState(P(0, 0)).RefreshEndsUtc);
        }

        [Test]
        public void CellStateIsSavedByName()
        {
            var clock = FakeClock.AtNoon();
            var s = EmptySession(clock);
            Put(s, 0, 0, "item.water.01");
            s.Board.SetState(P(0, 0), Core.Board.CellState.Withered);
            StringAssert.Contains("\"Withered\"", JsonConvert.SerializeObject(SaveMapper.Capture(s)));
        }
    }
}
