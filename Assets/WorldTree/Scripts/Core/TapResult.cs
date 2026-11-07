namespace WorldTree.Core
{
    /// <summary>Outcome of tapping a cell. Every non-<see cref="Spawned"/> result leaves state unchanged.</summary>
    public enum TapResult
    {
        Spawned,
        OutOfBounds,
        Empty,
        Locked,
        NotAGenerator,
        NoCharges,
        BoardFull,
        NotEnoughSap,
    }
}
