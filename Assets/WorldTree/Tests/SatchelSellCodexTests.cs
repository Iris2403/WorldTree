using System;
using NUnit.Framework;
using WorldTree.Core;
using WorldTree.Core.Board;
using WorldTree.Core.Content;
using WorldTree.Core.Economy;
using WorldTree.Tests.Support;
using static WorldTree.Tests.Support.TestContent;

namespace WorldTree.Tests
{
    /// <summary>GDD §3.4 Codex and §3.5 satchel, selling and undo.</summary>
    public class SatchelSellCodexTests
    {
        private FakeClock _clock;
        private GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = FakeClock.AtNoon();
            _s = EmptySession(_clock);
        }

        // ---------- Satchel ----------

        [Test]
        public void StoreMovesItemOffTheBoard()
        {
            Put(_s, 2, 2, "item.water.03");
            Assert.AreEqual(StoreResult.Stored, _s.StoreInSatchel(P(2, 2)));
            Assert.IsNull(_s.Board.GetItem(P(2, 2)));
            Assert.AreEqual("item.water.03", _s.Satchel.Items[0].ItemId);
        }

        [Test]
        public void SatchelHoldsFourAtStart()
        {
            for (int i = 0; i < 5; i++) Put(_s, i, 0, "item.water.01");
            for (int i = 0; i < 4; i++) Assert.AreEqual(StoreResult.Stored, _s.StoreInSatchel(P(i, 0)));
            long v = _s.StateVersion;
            Assert.AreEqual(StoreResult.SatchelFull, _s.StoreInSatchel(P(4, 0)));
            Assert.AreEqual("item.water.01", _s.Board.GetItem(P(4, 0)).ItemId);
            Assert.AreEqual(v, _s.StateVersion);
        }

        [Test]
        public void LockedOrEmptyCellsCannotBeStored()
        {
            Put(_s, 1, 1, "item.water.01");
            _s.Board.SetState(P(1, 1), CellState.Withered);
            Assert.AreEqual(StoreResult.Locked, _s.StoreInSatchel(P(1, 1)));
            Assert.AreEqual(StoreResult.Empty, _s.StoreInSatchel(P(2, 2)));
            Assert.AreEqual(StoreResult.OutOfBounds, _s.StoreInSatchel(P(-1, 0)));
        }

        [Test]
        public void RetrievePlacesNearestToBottomCentre()
        {
            Put(_s, 0, 0, "item.water.03");
            _s.StoreInSatchel(P(0, 0));
            Assert.AreEqual(RetrieveResult.Retrieved, _s.RetrieveFromSatchel(0, out var placed));
            Assert.AreEqual(P(3, 8), placed);
            Assert.AreEqual("item.water.03", _s.Board.GetItem(P(3, 8)).ItemId);
            Assert.AreEqual(0, _s.Satchel.Count);
        }

        [Test]
        public void RetrieveFailsWhenBoardIsFullOrSlotInvalid()
        {
            Put(_s, 0, 0, "item.water.03");
            _s.StoreInSatchel(P(0, 0));
            FillBoard(_s, "item.nature.08");
            Assert.AreEqual(RetrieveResult.BoardFull, _s.RetrieveFromSatchel(0, out _));
            Assert.AreEqual(1, _s.Satchel.Count);
            Assert.AreEqual(RetrieveResult.InvalidSlot, _s.RetrieveFromSatchel(3, out _));
        }

        [Test]
        public void GeneratorKeepsRefreshingWhileInSatchel()
        {
            Put(_s, 3, 4, PrototypeContent.SeedPouch1);
            for (int i = 0; i < 5; i++) _s.TapCell(P(3, 4));
            _s.StoreInSatchel(P(3, 4));
            Assert.AreEqual(55, _s.GetSatchelGeneratorState(0).Charges);
            _clock.AdvanceMinutes(20);
            Assert.AreEqual(60, _s.GetSatchelGeneratorState(0).Charges);
        }

        // ---------- Selling ----------

        [Test]
        public void SellPaysSellValueAndEmptiesCell()
        {
            Put(_s, 2, 2, "item.water.04"); // sell value 8
            Assert.AreEqual(SellResult.Sold, _s.SellItem(P(2, 2)));
            Assert.IsNull(_s.Board.GetItem(P(2, 2)));
            Assert.AreEqual(8, _s.Wallet.Glimmer);
        }

        [Test]
        public void PermanentGeneratorsCannotBeSold()
        {
            Put(_s, 2, 2, PrototypeContent.SeedPouch1);
            Assert.AreEqual(SellResult.NotSellable, _s.SellItem(P(2, 2)));
            Assert.IsNotNull(_s.Board.GetItem(P(2, 2)));
        }

        [Test]
        public void LockedItemsCannotBeSold()
        {
            Put(_s, 2, 2, "item.water.04");
            _s.Board.SetState(P(2, 2), CellState.Sealed);
            Assert.AreEqual(SellResult.Locked, _s.SellItem(P(2, 2)));
        }

        [Test]
        public void UndoWithinFiveSecondsRestoresItemAndTakesGlimmerBack()
        {
            Put(_s, 2, 2, "item.water.04");
            _s.SellItem(P(2, 2));
            _clock.AdvanceSeconds(4.9);
            Assert.AreEqual(UndoSellResult.Restored, _s.UndoLastSale());
            Assert.AreEqual("item.water.04", _s.Board.GetItem(P(2, 2)).ItemId);
            Assert.AreEqual(0, _s.Wallet.Glimmer);
            Assert.AreEqual(UndoSellResult.NothingToUndo, _s.UndoLastSale());
        }

        [Test]
        public void UndoAfterFiveSecondsFails()
        {
            Put(_s, 2, 2, "item.water.04");
            _s.SellItem(P(2, 2));
            _clock.Advance(EconomyRules.SellUndoWindow + TimeSpan.FromMilliseconds(1));
            Assert.AreEqual(UndoSellResult.Expired, _s.UndoLastSale());
            Assert.AreEqual(8, _s.Wallet.Glimmer);
            Assert.AreEqual(0, _s.SellUndoSecondsLeft);
        }

        [Test]
        public void UndoUsesNearestCellWhenOriginalIsTaken()
        {
            Put(_s, 2, 2, "item.water.04");
            _s.SellItem(P(2, 2));
            Put(_s, 2, 2, "item.nature.01");
            Assert.AreEqual(UndoSellResult.Restored, _s.UndoLastSale());
            Assert.AreEqual("item.water.04", _s.Board.GetItem(P(2, 1)).ItemId);
        }

        [Test]
        public void OnlyTheLastSaleCanBeUndone()
        {
            Put(_s, 1, 1, "item.water.02");
            Put(_s, 2, 2, "item.water.04");
            _s.SellItem(P(1, 1));
            _s.SellItem(P(2, 2));
            _s.UndoLastSale();
            Assert.IsNull(_s.Board.GetItem(P(1, 1)));
            Assert.AreEqual("item.water.04", _s.Board.GetItem(P(2, 2)).ItemId);
            Assert.AreEqual(2, _s.Wallet.Glimmer);
        }

        // ---------- Codex ----------

        [Test]
        public void FirstDiscoveryPaysGlimmerOnceAndRaisesEvent()
        {
            Put(_s, 0, 0, "item.water.02");
            Put(_s, 1, 0, "item.water.02");
            Put(_s, 0, 1, "item.water.02");
            Put(_s, 1, 1, "item.water.02");
            string discovered = null;
            int reward = 0;
            _s.ItemDiscovered += (id, r) => { discovered = id; reward = r; };
            _s.DragItem(P(0, 0), P(1, 0));
            Assert.AreEqual("item.water.03", discovered);
            Assert.AreEqual(3 * EconomyRules.DiscoveryGlimmerPerLevel, reward);
            Assert.AreEqual(reward, _s.Wallet.Glimmer);

            discovered = null;
            _s.DragItem(P(0, 1), P(1, 1));
            Assert.IsNull(discovered, "second time is not a discovery");
            Assert.AreEqual(reward, _s.Wallet.Glimmer);
        }

        [Test]
        public void NewGameDiscoversVisibleOpenItemsWithoutReward()
        {
            var s = GameSession.NewGame(TestContent.Catalog, PrototypeContent.StartingLayout(), PrototypeContent.Energy, _clock, 1);
            Assert.IsTrue(s.Codex.IsDiscovered(PrototypeContent.SeedPouch1));
            Assert.IsTrue(s.Codex.IsDiscovered("item.nature.01"));
            Assert.IsFalse(s.Codex.IsDiscovered(PrototypeContent.Stump1), "hidden under Sealed roots");
            Assert.IsFalse(s.Codex.IsDiscovered("item.craft.02"), "visible under blight but not yet freed");
            Assert.AreEqual(0, s.Wallet.Glimmer);
        }

        [Test]
        public void CleansingFreesAndDiscoversTheItem()
        {
            Put(_s, 3, 3, "item.craft.02");
            _s.Board.SetState(P(3, 3), CellState.Withered);
            Put(_s, 3, 4, "item.nature.01");
            Put(_s, 0, 8, "item.nature.01");
            _s.DragItem(P(0, 8), P(3, 4));
            Assert.IsTrue(_s.Codex.IsDiscovered("item.craft.02"));
        }

        [Test]
        public void SpawnedItemsAreDiscovered()
        {
            Put(_s, 3, 4, PrototypeContent.Moonwell1);
            var outcome = _s.TapCell(P(3, 4));
            Assert.IsTrue(_s.Codex.IsDiscovered(outcome.SpawnedItemId));
        }
    }
}
