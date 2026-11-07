namespace WorldTree.Core
{
    public enum SellResult
    {
        Sold,
        OutOfBounds,
        Empty,
        Locked,

        /// <summary>Permanent generators can't be sold (GDD §3.5).</summary>
        NotSellable,
    }
}
