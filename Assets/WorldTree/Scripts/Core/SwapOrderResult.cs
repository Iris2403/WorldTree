namespace WorldTree.Core
{
    public enum SwapOrderResult
    {
        Swapped,
        OrderNotFound,
        OnCooldown,

        /// <summary>Items were already delivered to this order, so it can't be swapped.</summary>
        HasProgress,
    }
}
