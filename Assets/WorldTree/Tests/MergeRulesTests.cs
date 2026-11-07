using NUnit.Framework;
using WorldTree.Core;
using WorldTree.Core.Board;
using WorldTree.Tests.Support;
using static WorldTree.Tests.Support.TestContent;

namespace WorldTree.Tests
{
    /// <summary>GDD §3.2 interaction table (Open cells) and §4.3 generator merging.</summary>
    public class MergeRulesTests
    {
        private FakeClock _clock;
        private GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = FakeClock.AtNoon();
            _s = EmptySession(_clock);
        }

        [Test]
        public void SameIdMergesIntoNextLevelInTargetCellAndFreesSource()
        {
            Put(_s, 0, 0, "item.nature.01");
            Put(_s, 1, 0, "item.nature.01");
            var d = _s.DragItem(P(0, 0), P(1, 0));
            Assert.AreEqual(MoveKind.Merge, d.Kind);
            Assert.IsNull(_s.Board.GetItem(P(0, 0)));
            Assert.AreEqual("item.nature.02", _s.Board.GetItem(P(1, 0)).ItemId);
            Assert.AreEqual(1, _s.Stats.Merges);
        }

        [Test]
        public void MaxLevelSameIdSwaps()
        {
            Put(_s, 0, 0, "item.nature.08");
            Put(_s, 1, 0, "item.nature.08");
            Assert.AreEqual(MoveKind.Swap, _s.DragItem(P(0, 0), P(1, 0)).Kind);
            Assert.AreEqual("item.nature.08", _s.Board.GetItem(P(0, 0)).ItemId);
            Assert.AreEqual("item.nature.08", _s.Board.GetItem(P(1, 0)).ItemId);
        }

        [Test]
        public void DifferentIdsSwap()
        {
            Put(_s, 0, 0, "item.nature.01");
            Put(_s, 1, 0, "item.water.01");
            Assert.AreEqual(MoveKind.Swap, _s.DragItem(P(0, 0), P(1, 0)).Kind);
            Assert.AreEqual("item.water.01", _s.Board.GetItem(P(0, 0)).ItemId);
            Assert.AreEqual("item.nature.01", _s.Board.GetItem(P(1, 0)).ItemId);
        }

        [Test]
        public void DifferentLevelsOfSameChainSwap()
        {
            Put(_s, 0, 0, "item.nature.01");
            Put(_s, 1, 0, "item.nature.02");
            Assert.AreEqual(MoveKind.Swap, _s.DragItem(P(0, 0), P(1, 0)).Kind);
        }

        [Test]
        public void DropOnEmptyCellMoves()
        {
            Put(_s, 0, 0, "item.water.03");
            Assert.AreEqual(MoveKind.Move, _s.DragItem(P(0, 0), P(4, 6)).Kind);
            Assert.IsNull(_s.Board.GetItem(P(0, 0)));
            Assert.AreEqual("item.water.03", _s.Board.GetItem(P(4, 6)).ItemId);
        }

        [Test]
        public void DropOnSelfIsNoOpAndDoesNotChangeVersion()
        {
            Put(_s, 2, 2, "item.water.01");
            long v = _s.StateVersion;
            Assert.AreEqual(MoveKind.NoOp, _s.DragItem(P(2, 2), P(2, 2)).Kind);
            Assert.AreEqual(v, _s.StateVersion);
        }

        [Test]
        public void OutOfBoundsTargetIsRejectedAndChangesNothing()
        {
            Put(_s, 0, 0, "item.water.01");
            long v = _s.StateVersion;
            var d = _s.DragItem(P(0, 0), P(-1, 0));
            Assert.AreEqual(MoveKind.Rejected, d.Kind);
            Assert.AreEqual(RejectReason.OutOfBounds, d.Reason);
            Assert.AreEqual("item.water.01", _s.Board.GetItem(P(0, 0)).ItemId);
            Assert.AreEqual(v, _s.StateVersion);
        }

        [Test]
        public void EmptySourceIsRejected()
        {
            Assert.AreEqual(RejectReason.SourceEmpty, _s.DragItem(P(0, 0), P(1, 0)).Reason);
        }

        [Test]
        public void LockedSourceAndTargetAreRejected()
        {
            Put(_s, 0, 0, "item.nature.01");
            Put(_s, 1, 0, "item.nature.01");
            _s.Board.SetState(P(1, 0), CellState.Sealed);
            Assert.AreEqual(RejectReason.TargetLocked, _s.DragItem(P(0, 0), P(1, 0)).Reason);
            Assert.AreEqual(RejectReason.SourceLocked, _s.DragItem(P(1, 0), P(0, 0)).Reason);
        }

        [Test]
        public void SuccessfulCommandsIncrementStateVersion()
        {
            Put(_s, 0, 0, "item.nature.01");
            long v = _s.StateVersion;
            _s.DragItem(P(0, 0), P(1, 0));
            Assert.AreEqual(v + 1, _s.StateVersion);
        }

        [Test]
        public void MergeRaisesEventWithResult()
        {
            Put(_s, 0, 0, "item.water.02");
            Put(_s, 0, 1, "item.water.02");
            string merged = null;
            _s.ItemsMerged += (from, to, result) => merged = result.ItemId;
            _s.DragItem(P(0, 0), P(0, 1));
            Assert.AreEqual("item.water.03", merged);
        }
    }
}
