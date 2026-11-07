namespace WorldTree.Core.Board
{
    /// <summary>Outcome of dragging an item onto a cell (GDD §3.2).</summary>
    public enum MoveKind
    {
        NoOp,
        Move,
        Merge,
        Swap,
        Rejected,
    }
}
