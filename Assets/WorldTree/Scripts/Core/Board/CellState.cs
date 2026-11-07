namespace WorldTree.Core.Board
{
    /// <summary>
    /// State of a board cell (GDD §3.1). Withered and Sealed cells always hold one locked item.
    /// Phase 1 boards use Open cells only; the Withered/Sealed rules (GDD §3.3) arrive in Phase 2.
    /// </summary>
    public enum CellState
    {
        Open = 0,
        Withered = 1,
        Sealed = 2,
    }
}
