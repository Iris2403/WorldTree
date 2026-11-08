using System.Collections.Generic;
using WorldTree.Core.Common;
using WorldTree.Core.Items;

namespace WorldTree.Core.Board
{
    /// <summary>
    /// GDD §3.3 cleansing and revealing after a merge with result cell R (revised in Phase 2, roadmap D30):
    /// cleanse = R if it was Withered (a matching item was dropped onto it), plus orthogonal Withered
    /// neighbours of R that hold a <b>generator</b>. A Withered normal item is never freed by a merge
    /// beside it. Reveal = Sealed cells orthogonally adjacent to any cleansed cell. Computed from the
    /// board state before the merge; one step per merge, no cascade.
    /// </summary>
    public static class Cleansing
    {
        private static readonly GridPos[] Directions =
        {
            new GridPos(0, -1), new GridPos(-1, 0), new GridPos(1, 0), new GridPos(0, 1),
        };

        /// <summary>
        /// Computes which cells change. Must be called <b>before</b> the merge mutates the board.
        /// Results are in row-major order, matching the event order in GDD §3.3 rule 6.
        /// </summary>
        public static void Compute(BoardModel board, ICatalog catalog, GridPos result, List<GridPos> cleansed, List<GridPos> revealed)
        {
            cleansed.Clear();
            revealed.Clear();
            if (board.GetState(result) == CellState.Withered) cleansed.Add(result);
            foreach (var d in Directions)
            {
                var n = new GridPos(result.X + d.X, result.Y + d.Y);
                if (board.IsInside(n) && board.GetState(n) == CellState.Withered && HoldsGenerator(board, catalog, n)) cleansed.Add(n);
            }

            foreach (var c in cleansed)
            {
                foreach (var d in Directions)
                {
                    var n = new GridPos(c.X + d.X, c.Y + d.Y);
                    if (board.IsInside(n) && board.GetState(n) == CellState.Sealed && !revealed.Contains(n)) revealed.Add(n);
                }
            }

            SortRowMajor(cleansed);
            SortRowMajor(revealed);
        }

        public static IEnumerable<GridPos> Neighbours(BoardModel board, GridPos p)
        {
            foreach (var d in Directions)
            {
                var n = new GridPos(p.X + d.X, p.Y + d.Y);
                if (board.IsInside(n)) yield return n;
            }
        }

        public static bool HoldsGenerator(BoardModel board, ICatalog catalog, GridPos p)
        {
            var item = board.GetItem(p);
            return item != null && catalog.GetItem(item.ItemId).IsGenerator;
        }

        private static void SortRowMajor(List<GridPos> list) =>
            list.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
    }
}
