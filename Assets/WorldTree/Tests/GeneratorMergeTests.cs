using NUnit.Framework;
using WorldTree.Core;
using WorldTree.Core.Board;
using WorldTree.Core.Content;
using WorldTree.Tests.Support;
using static WorldTree.Tests.Support.TestContent;

namespace WorldTree.Tests
{
    /// <summary>GDD §4.3 generator merging.</summary>
    public class GeneratorMergeTests
    {
        private FakeClock _clock;
        private GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = FakeClock.AtNoon();
            // A larger board, so two generators can be fully emptied without filling it.
            _s = EmptySession(_clock, 12, 12);
            _s.DebugAddSap(500);
        }

        private void Deplete(int x, int y, int taps)
        {
            for (int i = 0; i < taps; i++) Assert.AreEqual(TapResult.Spawned, _s.TapCell(P(x, y)).Result);
        }

        private void AssertFullLv2At(int x, int y)
        {
            var item = _s.Board.GetItem(P(x, y));
            Assert.AreEqual(PrototypeContent.SeedPouch2, item.ItemId);
            Assert.AreEqual(66, item.Generator.Charges);
            Assert.IsNull(item.Generator.RefreshEndsUtc);
        }

        [Test]
        public void FullPlusDepletedGivesFullLv2AndDiscardsTimer()
        {
            Put(_s, 0, 0, PrototypeContent.SeedPouch1);
            Put(_s, 11, 11, PrototypeContent.SeedPouch1);
            Deplete(0, 0, 60);
            Assert.AreEqual(MoveKind.Merge, _s.DragItem(P(11, 11), P(0, 0)).Kind);
            AssertFullLv2At(0, 0);
        }

        [Test]
        public void BothDepletedGivesFullLv2()
        {
            Put(_s, 0, 0, PrototypeContent.SeedPouch1);
            Put(_s, 11, 11, PrototypeContent.SeedPouch1);
            Deplete(0, 0, 60);
            Deplete(11, 11, 60);
            _s.DragItem(P(0, 0), P(11, 11));
            AssertFullLv2At(11, 11);
            Assert.IsNull(_s.Board.GetItem(P(0, 0)));
        }

        [Test]
        public void PartlyUsedGeneratorsGiveFullLv2()
        {
            Put(_s, 0, 0, PrototypeContent.SeedPouch1);
            Put(_s, 11, 11, PrototypeContent.SeedPouch1);
            Deplete(0, 0, 10);
            Deplete(11, 11, 25);
            _s.DragItem(P(0, 0), P(11, 11));
            AssertFullLv2At(11, 11);
        }

        [Test]
        public void DifferentGeneratorTypesSwap()
        {
            Put(_s, 0, 0, PrototypeContent.SeedPouch1);
            Put(_s, 1, 0, PrototypeContent.Moonwell1);
            Assert.AreEqual(MoveKind.Swap, _s.DragItem(P(0, 0), P(1, 0)).Kind);
        }

        [Test]
        public void GeneratorAndItemSwap()
        {
            Put(_s, 0, 0, PrototypeContent.SeedPouch1);
            Put(_s, 1, 0, "item.nature.01");
            Assert.AreEqual(MoveKind.Swap, _s.DragItem(P(0, 0), P(1, 0)).Kind);
        }

        [Test]
        public void MaxLevelGeneratorsSwap()
        {
            // Moonwell Spring has a single level in Phase 1, so it is at max level.
            Put(_s, 0, 0, PrototypeContent.Moonwell1);
            Put(_s, 1, 0, PrototypeContent.Moonwell1);
            Assert.AreEqual(MoveKind.Swap, _s.DragItem(P(0, 0), P(1, 0)).Kind);
            Put(_s, 2, 0, PrototypeContent.SeedPouch2);
            Put(_s, 3, 0, PrototypeContent.SeedPouch2);
            Assert.AreEqual(MoveKind.Swap, _s.DragItem(P(2, 0), P(3, 0)).Kind);
        }
    }
}
