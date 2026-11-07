using System.Collections.Generic;
using NUnit.Framework;
using WorldTree.Core;
using WorldTree.Core.Board;
using WorldTree.Core.Common;
using WorldTree.Core.Content;
using WorldTree.Tests.Support;
using static WorldTree.Tests.Support.TestContent;

namespace WorldTree.Tests
{
    /// <summary>GDD §3.2 (locked targets) and §3.3 cleansing and revealing, rule by rule.</summary>
    public class CleansingTests
    {
        private FakeClock _clock;
        private GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = FakeClock.AtNoon();
            _s = EmptySession(_clock);
        }

        private void Lock(int x, int y, string id, CellState state)
        {
            Put(_s, x, y, id);
            _s.Board.SetState(P(x, y), state);
        }

        private CellState State(int x, int y) => _s.Board.GetState(P(x, y));

        [Test]
        public void GddWorkedExample()
        {
            // Before (rows 0..2, columns 0..2):   After merging A onto B (B at (1,2)):
            //  S  S  S                              S  W  S
            //  .  W  S                              .  .  W
            //  .  B  W                              .  *  .
            Lock(0, 0, "item.water.01", CellState.Sealed);
            Lock(1, 0, "item.water.01", CellState.Sealed);
            Lock(2, 0, "item.water.01", CellState.Sealed);
            Lock(1, 1, "item.water.02", CellState.Withered);
            Lock(2, 1, "item.water.03", CellState.Sealed);
            Lock(2, 2, "item.water.04", CellState.Withered);
            Put(_s, 1, 2, "item.nature.01");
            Put(_s, 0, 5, "item.nature.01");

            Assert.AreEqual(MoveKind.Merge, _s.DragItem(P(0, 5), P(1, 2)).Kind);

            Assert.AreEqual(CellState.Sealed, State(0, 0));
            Assert.AreEqual(CellState.Withered, State(1, 0));
            Assert.AreEqual(CellState.Sealed, State(2, 0));
            Assert.AreEqual(CellState.Open, State(1, 1));
            Assert.AreEqual(CellState.Withered, State(2, 1));
            Assert.AreEqual(CellState.Open, State(2, 2));
            Assert.AreEqual("item.nature.02", _s.Board.GetItem(P(1, 2)).ItemId);
        }

        [Test]
        public void MergeBesideWitheredCleansesItAndFreesTheItem()
        {
            Lock(3, 3, "item.water.02", CellState.Withered);
            Put(_s, 3, 4, "item.nature.01");
            Put(_s, 0, 8, "item.nature.01");
            _s.DragItem(P(0, 8), P(3, 4));
            Assert.AreEqual(CellState.Open, State(3, 3));
            // The freed item can now be moved.
            Assert.AreEqual(MoveKind.Move, _s.DragItem(P(3, 3), P(6, 6)).Kind);
        }

        [Test]
        public void DiagonalNeighboursAreNotCleansed()
        {
            Lock(4, 3, "item.water.02", CellState.Withered);
            Put(_s, 3, 4, "item.nature.01");
            Put(_s, 0, 8, "item.nature.01");
            _s.DragItem(P(0, 8), P(3, 4));
            Assert.AreEqual(CellState.Withered, State(4, 3));
        }

        [Test]
        public void SourceCellNeighboursAreNotCleansed()
        {
            // Only the result cell's neighbours count; the source cell is vacated.
            Lock(0, 7, "item.water.02", CellState.Withered);
            Put(_s, 0, 8, "item.nature.01");
            Put(_s, 5, 5, "item.nature.01");
            _s.DragItem(P(0, 8), P(5, 5));
            Assert.AreEqual(CellState.Withered, State(0, 7));
        }

        [Test]
        public void CleansingMergeOntoWitheredItemOpensItsCell()
        {
            Lock(3, 3, "item.water.02", CellState.Withered);
            Put(_s, 0, 8, "item.water.02");
            var d = _s.DragItem(P(0, 8), P(3, 3));
            Assert.AreEqual(MoveKind.Merge, d.Kind);
            Assert.IsTrue(d.CleansingTarget);
            Assert.AreEqual(CellState.Open, State(3, 3));
            Assert.AreEqual("item.water.03", _s.Board.GetItem(P(3, 3)).ItemId);
            Assert.IsNull(_s.Board.GetItem(P(0, 8)));
        }

        [Test]
        public void CleansingMergeAlsoCleansesWitheredNeighboursAndRevealsAroundAllCleansedCells()
        {
            Lock(3, 3, "item.water.02", CellState.Withered);
            Lock(3, 2, "item.nature.05", CellState.Withered);
            Lock(3, 1, "item.craft.01", CellState.Sealed);
            Lock(2, 3, "item.craft.02", CellState.Sealed);
            Put(_s, 0, 8, "item.water.02");
            _s.DragItem(P(0, 8), P(3, 3));
            Assert.AreEqual(CellState.Open, State(3, 3));
            Assert.AreEqual(CellState.Open, State(3, 2));
            Assert.AreEqual(CellState.Withered, State(3, 1), "revealed next to the cleansed neighbour");
            Assert.AreEqual(CellState.Withered, State(2, 3), "revealed next to the cleansed result cell");
        }

        [Test]
        public void WitheredWithDifferentIdOrMaxLevelIsRejected()
        {
            Lock(3, 3, "item.water.02", CellState.Withered);
            Put(_s, 0, 8, "item.water.01");
            Assert.AreEqual(RejectReason.TargetLocked, _s.DragItem(P(0, 8), P(3, 3)).Reason);

            Lock(5, 3, "item.water.08", CellState.Withered);
            Put(_s, 1, 8, "item.water.08");
            Assert.AreEqual(RejectReason.TargetLocked, _s.DragItem(P(1, 8), P(5, 3)).Reason);
        }

        [Test]
        public void SealedIsRejectedEvenWithSameId()
        {
            Lock(3, 3, "item.water.02", CellState.Sealed);
            Put(_s, 0, 8, "item.water.02");
            Assert.AreEqual(RejectReason.TargetLocked, _s.DragItem(P(0, 8), P(3, 3)).Reason);
            Assert.AreEqual(CellState.Sealed, State(3, 3));
        }

        [Test]
        public void RevealOnlyHappensNextToCleansedCellsNotNextToTheResultCell()
        {
            Lock(4, 4, "item.water.02", CellState.Withered); // next to R = (3,4): cleansed
            Lock(4, 3, "item.water.03", CellState.Sealed);   // next to the cleansed (4,4): revealed
            Lock(3, 3, "item.water.04", CellState.Sealed);   // next to R only, R wasn't cleansed: stays Sealed
            Put(_s, 3, 4, "item.nature.01");
            Put(_s, 0, 8, "item.nature.01");
            _s.DragItem(P(0, 8), P(3, 4));
            Assert.AreEqual(CellState.Open, State(4, 4));
            Assert.AreEqual(CellState.Withered, State(4, 3));
            Assert.AreEqual(CellState.Sealed, State(3, 3));
        }

        [Test]
        public void CellRevealedNextToTheResultIsNotCleansedBySameMerge()
        {
            // GDD §3.3 rule 4: in a cleansing merge R itself is cleansed, so its Sealed neighbour is
            // revealed. Although that neighbour is next to R, it isn't cleansed by the same merge.
            Lock(3, 3, "item.water.02", CellState.Withered);
            Lock(3, 2, "item.craft.01", CellState.Sealed);
            Put(_s, 0, 8, "item.water.02");
            _s.DragItem(P(0, 8), P(3, 3));
            Assert.AreEqual(CellState.Withered, State(3, 2));
        }

        [Test]
        public void MovesSwapsAndSpawnsNeverCleanse()
        {
            Lock(3, 3, "item.water.02", CellState.Withered);
            Put(_s, 0, 8, "item.nature.01");
            Assert.AreEqual(MoveKind.Move, _s.DragItem(P(0, 8), P(3, 4)).Kind);   // move next to W
            Put(_s, 2, 3, "item.water.01");
            Assert.AreEqual(MoveKind.Swap, _s.DragItem(P(3, 4), P(2, 3)).Kind);   // swap: both cells are next to W
            Put(_s, 4, 4, PrototypeContent.SeedPouch1);
            Assert.AreEqual(P(4, 3), _s.TapCell(P(4, 4)).SpawnPos);               // spawn lands next to W
            Assert.AreEqual(CellState.Withered, State(3, 3));
        }

        [Test]
        public void GeneratorMergeCleansesToo()
        {
            Lock(3, 3, "item.water.02", CellState.Withered);
            Put(_s, 3, 4, PrototypeContent.SeedPouch1);
            Put(_s, 0, 8, PrototypeContent.SeedPouch1);
            _s.DragItem(P(0, 8), P(3, 4));
            Assert.AreEqual(CellState.Open, State(3, 3));
        }

        [Test]
        public void StateChangeEventsFollowMergeEventInRowMajorOrder()
        {
            Lock(3, 3, "item.water.02", CellState.Withered);
            Lock(2, 4, "item.water.02", CellState.Withered);
            Lock(3, 2, "item.water.03", CellState.Sealed);
            Lock(1, 4, "item.water.03", CellState.Sealed);
            Put(_s, 3, 4, "item.nature.01");
            Put(_s, 0, 8, "item.nature.01");
            var log = new List<string>();
            _s.ItemsMerged += (f, t, r) => log.Add("merged");
            _s.CellStateChanged += (p, st) => log.Add(p + " " + st);
            _s.DragItem(P(0, 8), P(3, 4));
            CollectionAssert.AreEqual(new[]
            {
                "merged",
                "(3, 2) Withered",
                "(3, 3) Open",
                "(1, 4) Withered",
                "(2, 4) Open",
            }, log);
        }

        [Test]
        public void LockedItemsCannotBeTappedOrDragged()
        {
            Lock(3, 3, PrototypeContent.SeedPouch1, CellState.Withered);
            Assert.AreEqual(TapResult.Locked, _s.TapCell(P(3, 3)).Result);
            Assert.AreEqual(RejectReason.SourceLocked, _s.DragItem(P(3, 3), P(0, 0)).Reason);
        }

        [Test]
        public void SpawnsNeverLandOnLockedCells()
        {
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 7; x++)
                    if (!(x == 3 && y == 4)) Lock(x, y, "item.water.01", y == 8 && x == 6 ? CellState.Open : CellState.Withered);
            _s.Board.SetItem(P(6, 8), null);
            Put(_s, 3, 4, PrototypeContent.SeedPouch1);
            Assert.AreEqual(P(6, 8), _s.TapCell(P(3, 4)).SpawnPos);
            Assert.AreEqual(TapResult.BoardFull, _s.TapCell(P(3, 4)).Result);
        }
    }
}
