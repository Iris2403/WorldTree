using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using WorldTree.Core;
using WorldTree.Core.Board;
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
            var seedPouch = P(3, 4);
            for (int i = 0; i < 6; i++) Assert.AreEqual(TapResult.Spawned, s.TapCell(seedPouch).Result);
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

        [Test]
        public void Phase2StateRoundTrips()
        {
            var clock = FakeClock.AtNoon();
            var s = GameSession.NewGame(TestContent.Catalog, PrototypeContent.StartingLayout(), PrototypeContent.Energy, clock, 99);
            // Merge beside the Withered Moonwell Spring at (3,2): it's a generator, so it is freed.
            Put(s, 2, 3, "item.nature.01");
            Put(s, 3, 3, "item.nature.01");
            Assert.AreEqual(MoveKind.Merge, s.DragItem(P(2, 3), P(3, 3)).Kind);
            Assert.AreEqual(CellState.Open, s.Board.GetState(P(3, 2)));
            // Drop a matching item onto the Withered Nature L1 at (2,2): it is freed and (2,1) above is revealed.
            Put(s, 2, 5, "item.nature.01");
            Assert.AreEqual(MoveKind.Merge, s.DragItem(P(2, 5), P(2, 2)).Kind);
            Assert.AreEqual(CellState.Open, s.Board.GetState(P(2, 2)));
            Assert.AreEqual(CellState.Withered, s.Board.GetState(P(2, 1)));
            Assert.AreEqual(StoreResult.Stored, s.StoreInSatchel(P(3, 2)));
            Put(s, 4, 5, "item.water.01");
            Assert.AreEqual(SellResult.Sold, s.SellItem(P(4, 5)));

            var restored = Reload(s, clock);
            Assert.AreEqual(Snapshot(s), Snapshot(restored));
            Assert.AreEqual(s.Wallet.Glimmer, restored.Wallet.Glimmer);
            Assert.AreEqual(PrototypeContent.Moonwell1, restored.Satchel.Items[0].ItemId);
            Assert.IsTrue(restored.Codex.IsDiscovered("item.nature.02"));
            Assert.AreEqual(CellState.Withered, restored.Board.GetState(P(2, 1)));
        }

        [Test]
        public void Phase1SaveWithoutPhase2FieldsStillLoads()
        {
            // A save written by Phase 1 code: no Glimmer, Satchel or Discovered fields.
            const string payload = "{\"Width\":7,\"Height\":9,\"Cells\":[{\"X\":2,\"Y\":4,\"State\":\"Open\",\"ItemId\":\"gen.seed_pouch.1\",\"Charges\":50,\"RefreshEndsUtcTicks\":639030000000000000}],\"Sap\":80,\"RandomState\":12345}";
            var data = JsonConvert.DeserializeObject<SaveData>(payload);
            var clock = FakeClock.AtNoon();
            var s = SaveMapper.Restore(data, TestContent.Catalog, PrototypeContent.Energy, clock, new List<string>());
            Assert.AreEqual(0, s.Wallet.Glimmer);
            Assert.AreEqual(0, s.Satchel.Count);
            Assert.IsTrue(s.Codex.IsDiscovered(PrototypeContent.SeedPouch1), "visible items are discovered on load");
            Assert.AreEqual(80, s.Energy.Amount);
        }
    }
}
