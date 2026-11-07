using System.Collections.Generic;
using WorldTree.Core.Common;

namespace WorldTree.Core.Board
{
    /// <summary>
    /// GDD §3.3 cleansing and revealing after a merge with result cell R:
    /// cleanse = R (if Withered) plus its orthogonal Withered neighbours, computed from the board
    /// state before the merge; reveal = Sealed cells orthogonally adjacent to any cleansed cell.
    /// One step per merge, no cascade.
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
        public static void Compute(BoardModel board, GridPos result, List<GridPos> cleansed, List<GridPos> revealed)
        {
            cleansed.Clear();
            revealed.Clear();
            if (board.GetState(result) == CellState.Withered) cleansed.Add(result);
            foreach (var d in Directions)
            {
                var n = new GridPos(result.X + d.X, result.Y + d.Y);
                if (board.IsInside(n) && board.GetState(n) == CellState.Withered) cleansed.Add(n);
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

        private static void SortRowMajor(List<GridPos> list) =>
            list.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
    }
}
