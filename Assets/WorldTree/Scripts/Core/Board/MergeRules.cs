using WorldTree.Core.Common;
using WorldTree.Core.Items;

namespace WorldTree.Core.Board
{
    /// <summary>
    /// Pure decision function for GDD §3.2 (Open cells) and §4.3 (generator merging). Generators use
    /// the same table: same ID below max level merges, everything else swaps.
    /// Phase 2 adds the cleansing merge onto Withered items (GDD §3.3); until then locked targets are rejected.
    /// </summary>
    public static class MergeRules
    {
        public static MoveDecision Resolve(BoardModel board, ICatalog catalog, GridPos from, GridPos to)
        {
            if (!board.IsInside(from)) return MoveDecision.Reject(RejectReason.OutOfBounds);
            var source = board.GetItem(from);
            if (source == null) return MoveDecision.Reject(RejectReason.SourceEmpty);
            if (board.GetState(from) != CellState.Open) return MoveDecision.Reject(RejectReason.SourceLocked);
            if (!board.IsInside(to)) return MoveDecision.Reject(RejectReason.OutOfBounds);
            if (from == to) return MoveDecision.Of(MoveKind.NoOp);
            if (board.GetState(to) != CellState.Open) return MoveDecision.Reject(RejectReason.TargetLocked);

            var target = board.GetItem(to);
            if (target == null) return MoveDecision.Of(MoveKind.Move);

            if (source.ItemId == target.ItemId)
            {
                var def = catalog.GetItem(source.ItemId);
                if (!def.IsMaxLevel) return MoveDecision.MergeInto(def.NextId);
            }
            return MoveDecision.Of(MoveKind.Swap);
        }
    }
}
