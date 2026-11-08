using System.Collections.Generic;
using NUnit.Framework;
using WorldTree.Core.Board;
using WorldTree.Core.Content;
using WorldTree.Tests.Support;
using static WorldTree.Tests.Support.TestContent;

namespace WorldTree.Tests
{
    /// <summary>Authoring rules for starting boards (GDD §3.3, §4.2).</summary>
    public class LayoutValidatorTests
    {
        private static readonly LayoutPlacement[] TwoGenerators =
        {
            new LayoutPlacement(P(0, 8), PrototypeContent.SeedPouch1),
            new LayoutPlacement(P(1, 8), PrototypeContent.Moonwell1),
        };

        private static List<string> Validate(params LayoutPlacement[] extra)
        {
            var cells = new List<LayoutPlacement>(TwoGenerators);
            cells.AddRange(extra);
            return BoardLayoutValidator.Validate(new BoardLayout(7, 9, cells), TestContent.Catalog);
        }

        [Test]
        public void PrototypeStartingLayoutIsValid()
        {
            CollectionAssert.IsEmpty(BoardLayoutValidator.Validate(PrototypeContent.StartingLayout(), TestContent.Catalog));
        }

        [Test]
        public void SimpleOpenLayoutIsValid() => CollectionAssert.IsEmpty(Validate());

        [Test]
        public void TooFewGuaranteedChargesIsAnError()
        {
            var errors = BoardLayoutValidator.Validate(
                new BoardLayout(7, 9, new[] { new LayoutPlacement(P(0, 0), PrototypeContent.SeedPouch1) }), TestContent.Catalog);
            Assert.That(errors, Has.Some.Contains("charges"));
        }

        [Test]
        public void GeneratorsUnderBlightDoNotCountAsGuaranteed()
        {
            var errors = BoardLayoutValidator.Validate(new BoardLayout(7, 9, new[]
            {
                new LayoutPlacement(P(0, 8), PrototypeContent.SeedPouch1),
                new LayoutPlacement(P(3, 3), PrototypeContent.Moonwell1, CellState.Withered),
            }), TestContent.Catalog);
            Assert.That(errors, Has.Some.Contains("charges"));
        }

        [Test]
        public void SealedRegionWithoutWitheredCellCanNeverStartUnlocking()
        {
            var errors = Validate(new LayoutPlacement(P(3, 3), "item.water.01", CellState.Sealed));
            Assert.That(errors, Has.Some.Contains("can never start unlocking"));
        }

        [Test]
        public void SealedBehindWitheredItemIsValid()
        {
            CollectionAssert.IsEmpty(Validate(
                new LayoutPlacement(P(3, 1), "item.water.01", CellState.Withered),
                new LayoutPlacement(P(3, 0), "item.water.01", CellState.Sealed)));
        }

        [Test]
        public void WitheredItemFarFromOpenCellsIsAValidEntryPoint()
        {
            // A matching item can be dropped onto it from anywhere.
            CollectionAssert.IsEmpty(Validate(
                new LayoutPlacement(P(3, 3), "item.water.01", CellState.Withered),
                new LayoutPlacement(P(3, 2), "item.water.01", CellState.Sealed),
                new LayoutPlacement(P(3, 4), "item.water.01", CellState.Sealed),
                new LayoutPlacement(P(2, 3), "item.water.01", CellState.Sealed),
                new LayoutPlacement(P(4, 3), "item.water.01", CellState.Sealed)));
        }

        [Test]
        public void RegionWhoseOnlyWitheredCellIsAnEnclosedGeneratorIsAnError()
        {
            // A Withered generator is freed only by merging beside it, which needs an Open neighbour.
            var errors = Validate(
                new LayoutPlacement(P(3, 3), PrototypeContent.Stump1, CellState.Withered),
                new LayoutPlacement(P(3, 2), "item.water.01", CellState.Sealed),
                new LayoutPlacement(P(3, 4), "item.water.01", CellState.Sealed),
                new LayoutPlacement(P(2, 3), "item.water.01", CellState.Sealed),
                new LayoutPlacement(P(4, 3), "item.water.01", CellState.Sealed));
            Assert.That(errors, Has.Some.Contains("can never start unlocking"));
        }

        [Test]
        public void LockedMaxLevelItemIsAnError()
        {
            var errors = Validate(new LayoutPlacement(P(3, 3), "item.water.08", CellState.Withered));
            Assert.That(errors, Has.Some.Contains("max-level"));
        }

        [Test]
        public void LockedItemOfAChainWithoutGeneratorOnTheBoardIsAllowed()
        {
            // Chains can become obtainable later in the game (roadmap D30).
            CollectionAssert.IsEmpty(Validate(new LayoutPlacement(P(3, 3), "item.craft.02", CellState.Withered)));
        }

        [Test]
        public void LockedCellWithoutItemUnknownItemAndDuplicatesAreErrors()
        {
            var errors = Validate(
                new LayoutPlacement(P(3, 3), null, CellState.Withered),
                new LayoutPlacement(P(4, 4), "item.unknown.01"),
                new LayoutPlacement(P(5, 5), "item.water.01"),
                new LayoutPlacement(P(5, 5), "item.water.02"),
                new LayoutPlacement(P(9, 9), "item.water.01"));
            Assert.That(errors, Has.Some.Contains("locked cells must hold an item"));
            Assert.That(errors, Has.Some.Contains("Unknown item"));
            Assert.That(errors, Has.Some.Contains("Two placements"));
            Assert.That(errors, Has.Some.Contains("outside the board"));
        }

        [Test]
        public void TooFewOpenCellsIsAnError()
        {
            var cells = new List<LayoutPlacement>();
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 7; x++)
                    if (y < 8 || x > 3) cells.Add(new LayoutPlacement(P(x, y), "item.water.01", CellState.Withered));
            cells.Add(new LayoutPlacement(P(0, 8), PrototypeContent.SeedPouch1));
            cells.Add(new LayoutPlacement(P(1, 8), PrototypeContent.Moonwell1));
            var errors = BoardLayoutValidator.Validate(new BoardLayout(7, 9, cells), TestContent.Catalog);
            Assert.That(errors, Has.Some.Contains("Open cells"));
        }
    }
}
