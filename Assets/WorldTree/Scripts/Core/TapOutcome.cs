using WorldTree.Core.Common;

namespace WorldTree.Core
{
    public readonly struct TapOutcome
    {
        public TapOutcome(TapResult result, GridPos spawnPos, string spawnedItemId)
        {
            Result = result;
            SpawnPos = spawnPos;
            SpawnedItemId = spawnedItemId;
        }

        public TapResult Result { get; }

        /// <summary>Where the new item was placed (only when <see cref="Result"/> is Spawned).</summary>
        public GridPos SpawnPos { get; }

        public string SpawnedItemId { get; }

        public static TapOutcome Fail(TapResult result) => new TapOutcome(result, default, null);
    }
}
