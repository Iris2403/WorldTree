using System.Collections.Generic;
using NUnit.Framework;
using WorldTree.Core;
using WorldTree.Core.Board;
using WorldTree.Core.Common;
using WorldTree.Core.Content;
using WorldTree.Tests.Support;

namespace WorldTree.Tests
{
    /// <summary>
    /// A simple greedy player shows the starting board (roadmap D31) can't get stuck in the small
    /// centre and can be opened completely by merging outward. Sap and refresh timers are bypassed
    /// with debug actions: this checks the board design, not the energy balance.
    /// </summary>
    public class StartingBoardPlaythroughTests
    {
        [TestCase(1u)]
        [TestCase(42u)]
        [TestCase(2026u)]
        public void GreedyPlayerOpensTheWholeBoard(uint seed)
        {
            var clock = FakeClock.AtNoon();
            var s = GameSession.NewGame(TestContent.Catalog, PrototypeContent.StartingLayout(), PrototypeContent.Energy, clock, seed);
            int moonwellFreedAtStep = -1;
            int steps = 0;

            for (int step = 0; step < 20000 && CountOpen(s) < 63; step++)
            {
                steps = step + 1;
                if (moonwellFreedAtStep < 0 && s.Board.GetState(new GridPos(3, 2)) == CellState.Open) moonwellFreedAtStep = step;
                if (TryCleansingDrop(s) || TryMergeBesideWitheredGenerator(s) || TryAnyMerge(s) || TryTapGenerator(s)) continue;
                // Board full and nothing mergeable: sell the lowest-level item to make room (GDD §3.5).
                Assert.IsTrue(SellLowest(s), "stuck at step " + step + " with " + CountOpen(s) + " open cells");
            }

            if (CountOpen(s) < 63)
            {
                var sb = new System.Text.StringBuilder();
                for (int y = 0; y < 9; y++)
                {
                    for (int x = 0; x < 7; x++)
                    {
                        var p = new GridPos(x, y);
                        var it = s.Board.GetItem(p);
                        string st = s.Board.GetState(p) == CellState.Open ? "o" : s.Board.GetState(p) == CellState.Withered ? "W" : "S";
                        sb.Append((st + ":" + (it == null ? "-" : it.ItemId.Replace("item.", "").Replace("gen.", "G."))).PadRight(26));
                    }
                    sb.AppendLine();
                }
                TestContext.WriteLine(sb.ToString());
                System.Console.WriteLine(sb.ToString());
            }
            TestContext.WriteLine("seed " + seed + ": board fully open after " + steps + " actions (" + s.Stats.Taps + " taps, "
                + s.Stats.Merges + " merges); Moonwell Spring freed at action " + moonwellFreedAtStep);
            Assert.AreEqual(63, CountOpen(s), "the whole board opens");
            Assert.That(moonwellFreedAtStep, Is.InRange(0, 60), "the second generator unlocks early");
        }

        private static int CountOpen(GameSession s)
        {
            int n = 0;
            ForEachCell(s, p => { if (s.Board.GetState(p) == CellState.Open) n++; });
            return n;
        }

        private static bool TryCleansingDrop(GameSession s)
        {
            foreach (var target in Cells(s))
            {
                if (s.Board.GetState(target) != CellState.Withered) continue;
                var locked = s.Board.GetItem(target);
                foreach (var source in Cells(s))
                {
                    var item = s.Board.GetItem(source);
                    if (item != null && s.Board.GetState(source) == CellState.Open && item.ItemId == locked.ItemId
                        && s.DragItem(source, target).Kind == MoveKind.Merge) return true;
                }
            }
            return false;
        }

        private static bool TryMergeBesideWitheredGenerator(GameSession s)
        {
            foreach (var g in Cells(s))
            {
                var gen = s.Board.GetItem(g);
                if (s.Board.GetState(g) != CellState.Withered || !s.Catalog.GetItem(gen.ItemId).IsGenerator) continue;
                foreach (var spot in Cleansing.Neighbours(s.Board, g))
                {
                    if (s.Board.GetState(spot) != CellState.Open) continue;
                    var here = s.Board.GetItem(spot);
                    foreach (var source in Cells(s))
                    {
                        if (source == spot || s.Board.GetState(source) != CellState.Open) continue;
                        var item = s.Board.GetItem(source);
                        if (item == null) continue;
                        if (here == null)
                        {
                            // Bring an item next to the generator so a later merge lands beside it.
                            foreach (var other in Cells(s))
                            {
                                var o = s.Board.GetItem(other);
                                if (other != source && o != null && s.Board.GetState(other) == CellState.Open && o.ItemId == item.ItemId
                                    && !s.Catalog.GetItem(item.ItemId).IsMaxLevel)
                                {
                                    s.DragItem(source, spot);
                                    return s.DragItem(other, spot).Kind == MoveKind.Merge;
                                }
                            }
                        }
                        else if (item.ItemId == here.ItemId && s.DragItem(source, spot).Kind == MoveKind.Merge) return true;
                    }
                }
            }
            return false;
        }

        private static bool TryAnyMerge(GameSession s)
        {
            foreach (var a in Cells(s))
            {
                var item = s.Board.GetItem(a);
                if (item == null || s.Board.GetState(a) != CellState.Open) continue;
                foreach (var b in Cells(s))
                {
                    var other = s.Board.GetItem(b);
                    if (a != b && other != null && s.Board.GetState(b) == CellState.Open && other.ItemId == item.ItemId
                        && s.DragItem(a, b).Kind == MoveKind.Merge) return true;
                }
            }
            return false;
        }

        /// <summary>Taps the generator whose chain the most Withered items need, like a player working toward the blight.</summary>
        private static bool TryTapGenerator(GameSession s)
        {
            if (s.Sap < 1) s.DebugAddSap(100);
            GridPos? best = null;
            int bestNeed = -1;
            foreach (var p in Cells(s))
            {
                var g = s.GetGeneratorState(p);
                if (g == null || s.Board.GetState(p) != CellState.Open) continue;
                string dropChain = s.Catalog.GetItem(s.Catalog.GetItem(s.Board.GetItem(p).ItemId).Generator.Drops.Entries[0].ItemId).ChainId;
                int need = 0;
                foreach (var c in Cells(s))
                {
                    var locked = s.Board.GetItem(c);
                    if (locked != null && s.Board.GetState(c) == CellState.Withered && s.Catalog.GetItem(locked.ItemId).ChainId == dropChain) need++;
                }
                if (need > bestNeed) { bestNeed = need; best = p; }
            }
            if (!best.HasValue) return false;
            if (s.GetGeneratorState(best.Value).Charges == 0) s.DebugRefreshAllGenerators();
            return s.TapCell(best.Value).Result == TapResult.Spawned;
        }

        /// <summary>Sells max-level items first (nothing can merge with them), otherwise the lowest level.</summary>
        private static bool SellLowest(GameSession s)
        {
            GridPos? best = null;
            int bestScore = int.MaxValue;
            foreach (var p in Cells(s))
            {
                var item = s.Board.GetItem(p);
                if (item == null || s.Board.GetState(p) != CellState.Open) continue;
                var def = s.Catalog.GetItem(item.ItemId);
                if (def.IsGenerator) continue;
                int score = def.IsMaxLevel ? -1 : def.Level;
                if (score < bestScore) { bestScore = score; best = p; }
            }
            return best.HasValue && s.SellItem(best.Value) == SellResult.Sold;
        }

        private static IEnumerable<GridPos> Cells(GameSession s)
        {
            for (int y = 0; y < s.Board.Height; y++)
                for (int x = 0; x < s.Board.Width; x++)
                    yield return new GridPos(x, y);
        }

        private static void ForEachCell(GameSession s, System.Action<GridPos> action)
        {
            foreach (var p in Cells(s)) action(p);
        }
    }
}
