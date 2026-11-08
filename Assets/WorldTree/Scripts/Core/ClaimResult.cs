namespace WorldTree.Core
{
    public enum ClaimResult
    {
        Claimed,
        AlreadyClaimedToday,

        /// <summary>Item rewards don't fit on the board or in the satchel; nothing was granted.</summary>
        NoRoom,
    }
}
