using WorldTree.Core.Common;
using WorldTree.Core.Items;

namespace WorldTree.Core.Board
{
    /// <summary>
    /// Pure decision function for the GDD §3.2 interaction table, including the cleansing merge onto a
    /// Withered item, and §4.3 generator merging (generators use the same table). It only decides;
    /// <see cref="GameSession.DragItem"/> applies the result, including cleansing and revealing (§3.3).
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

            var targetState = board.GetState(to);
            var target = board.GetItem(to);

            if (targetState == CellState.Sealed) return MoveDecision.Reject(RejectReason.TargetLocked);

            if (targetState == CellState.Withered)
            {
                // Same ID below max level: cleansing merge. Anything else bounces off the blight.
                if (target != null && source.ItemId == target.ItemId && !catalog.GetItem(source.ItemId).IsMaxLevel)
                    return MoveDecision.CleanseInto(catalog.GetItem(source.ItemId).NextId);
                return MoveDecision.Reject(RejectReason.TargetLocked);
            }

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
