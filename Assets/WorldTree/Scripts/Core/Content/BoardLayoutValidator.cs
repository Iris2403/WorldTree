using System.Collections.Generic;
using WorldTree.Core.Board;
using WorldTree.Core.Common;
using WorldTree.Core.Items;

namespace WorldTree.Core.Content
{
    /// <summary>
    /// Authoring rules for starting boards (GDD §3.3, §4.2). Returns human-readable errors; an empty
    /// list means the layout is valid. Used by tests, the Editor "Validate Data" menu and at boot.
    /// </summary>
    public static class BoardLayoutValidator
    {
        public const int MinOpenCells = 6;

        /// <summary>The generators on Open cells must hold at least this many charges together (GDD §4.2).</summary>
        public const int MinGuaranteedCharges = 120;

        public static List<string> Validate(BoardLayout layout, ICatalog catalog)
        {
            var errors = new List<string>();
            var board = new BoardModel(layout.Width, layout.Height);
            var seen = new HashSet<GridPos>();

            foreach (var p in layout.Placements)
            {
                if (!board.IsInside(p.Pos))
                {
                    errors.Add("Placement outside the board: " + p.Pos);
                    continue;
                }
                if (!seen.Add(p.Pos)) errors.Add("Two placements in one cell: " + p.Pos);
                if (p.ItemId == null)
                {
                    errors.Add("Placement without an item at " + p.Pos + (p.State != CellState.Open ? " (locked cells must hold an item)" : ""));
                    continue;
                }
                if (!catalog.TryGetItem(p.ItemId, out _))
                {
                    errors.Add("Unknown item '" + p.ItemId + "' at " + p.Pos);
                    continue;
                }
                board.SetItem(p.Pos, new ItemInstance(p.ItemId));
                board.SetState(p.Pos, p.State);
            }

            int open = 0;
            int guaranteedCharges = 0;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    if (board.GetState(pos) != CellState.Open) continue;
                    open++;
                    var item = board.GetItem(pos);
                    if (item != null && catalog.GetItem(item.ItemId).IsGenerator)
                        guaranteedCharges += catalog.GetItem(item.ItemId).Generator.Capacity;
                }
            }
            if (open < MinOpenCells) errors.Add("Only " + open + " Open cells; at least " + MinOpenCells + " are required");
            if (guaranteedCharges < MinGuaranteedCharges)
                errors.Add("Generators on Open cells hold " + guaranteedCharges + " charges; at least " + MinGuaranteedCharges + " are required (GDD §4.2)");

            CheckLockedRegionsCanUnlock(board, errors);
            return errors;
        }

        /// <summary>
        /// Exact condition for the whole board to be unlockable: every connected region of locked cells
        /// must contain a Withered cell next to an Open cell. Merging beside that cell cleanses it, which
        /// reveals its Sealed neighbours and opens new merge spots, so the cleansing spreads through the
        /// whole region one step at a time. A region without such a cell can never start unlocking.
        /// </summary>
        private static void CheckLockedRegionsCanUnlock(BoardModel board, List<string> errors)
        {
            var visited = new HashSet<GridPos>();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var start = new GridPos(x, y);
                    if (board.GetState(start) == CellState.Open || visited.Contains(start)) continue;

                    bool hasEntryPoint = false;
                    var queue = new Queue<GridPos>();
                    queue.Enqueue(start);
                    visited.Add(start);
                    while (queue.Count > 0)
                    {
                        var c = queue.Dequeue();
                        foreach (var n in Cleansing.Neighbours(board, c))
                        {
                            if (board.GetState(n) == CellState.Open)
                            {
                                if (board.GetState(c) == CellState.Withered) hasEntryPoint = true;
                            }
                            else if (visited.Add(n))
                            {
                                queue.Enqueue(n);
                            }
                        }
                    }
                    if (!hasEntryPoint)
                        errors.Add("Locked region containing " + start + " can never be unlocked: it needs a Withered cell next to an Open cell");
                }
            }
        }
    }
}
