namespace WorldTree.Core
{
    public enum RestoreResult
    {
        Restored,

        /// <summary>The task was the area's last: the area is restored and the next one opens.</summary>
        AreaRestored,

        NotEnoughLifebloom,

        /// <summary>Every area is already restored, or the session has no story content.</summary>
        NothingToRestore,
    }
}
