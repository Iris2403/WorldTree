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
                    var item = board.GetItem(pos);
                    var state = board.GetState(pos);
                    if (state == CellState.Open)
                    {
                        open++;
                        if (item != null && catalog.GetItem(item.ItemId).IsGenerator)
                            guaranteedCharges += catalog.GetItem(item.ItemId).Generator.Capacity;
                        continue;
                    }
                    if (item != null) CheckLockedItemCanBeFreed(pos, catalog.GetItem(item.ItemId), errors);
                }
            }
            if (open < MinOpenCells) errors.Add("Only " + open + " Open cells; at least " + MinOpenCells + " are required");
            if (guaranteedCharges < MinGuaranteedCharges)
                errors.Add("Generators on Open cells hold " + guaranteedCharges + " charges; at least " + MinGuaranteedCharges + " are required (GDD §4.2)");

            CheckLockedRegionsCanStartUnlocking(board, catalog, errors);
            return errors;
        }

        /// <summary>
        /// A locked normal item is freed only by dropping a matching item onto it, so a max-level item
        /// (which nothing can merge with) could never be freed. Whether its chain is obtainable yet is not
        /// checked: chains may become available later in the game (roadmap D30).
        /// Locked generators are freed by merging beside them, so they need no matching item.
        /// </summary>
        private static void CheckLockedItemCanBeFreed(GridPos pos, ItemDef def, List<string> errors)
        {
            if (!def.IsGenerator && def.IsMaxLevel)
                errors.Add("Locked max-level item '" + def.Id + "' at " + pos + " can never be freed (nothing can merge with it)");
        }

        /// <summary>
        /// Every connected region of locked cells needs a cell that can be freed right away: a Withered
        /// normal item (drop a match onto it, from anywhere), or a Withered generator next to an Open cell
        /// (merge beside it). Freeing a cell reveals its Sealed neighbours, so the region opens step by step.
        /// </summary>
        private static void CheckLockedRegionsCanStartUnlocking(BoardModel board, ICatalog catalog, List<string> errors)
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
                        if (board.GetState(c) == CellState.Withered)
                        {
                            bool generator = Cleansing.HoldsGenerator(board, catalog, c);
                            if (!generator) hasEntryPoint = true;
                            else
                                foreach (var n in Cleansing.Neighbours(board, c))
                                    if (board.GetState(n) == CellState.Open) hasEntryPoint = true;
                        }
                        foreach (var n in Cleansing.Neighbours(board, c))
                        {
                            if (board.GetState(n) != CellState.Open && visited.Add(n)) queue.Enqueue(n);
                        }
                    }
                    if (!hasEntryPoint)
                        errors.Add("Locked region containing " + start + " can never start unlocking: it needs a Withered item, or a Withered generator next to an Open cell");
                }
            }
        }
    }
}
