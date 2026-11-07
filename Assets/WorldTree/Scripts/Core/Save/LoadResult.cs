namespace WorldTree.Core.Save
{
    public sealed class LoadResult
    {
        public LoadResult(SaveData data, LoadSource source, long sequence, bool restoreFailed, bool quarantined)
        {
            Data = data;
            Source = source;
            Sequence = sequence;
            RestoreFailed = restoreFailed;
            Quarantined = quarantined;
        }

        /// <summary>The newest valid save, or null when a new game must start.</summary>
        public SaveData Data { get; }

        public LoadSource Source { get; }
        public long Sequence { get; }

        /// <summary>Save files existed but none was valid: start a new game and tell the player once.</summary>
        public bool RestoreFailed { get; }

        /// <summary>An invalid file was kept as <c>save.corrupt.json</c>.</summary>
        public bool Quarantined { get; }
    }
}
