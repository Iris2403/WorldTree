namespace WorldTree.Core
{
    public enum DeliverResult
    {
        /// <summary>The order was completed and its rewards granted.</summary>
        Completed,

        /// <summary>A dragged item was accepted; the order still needs more.</summary>
        Accepted,

        OrderNotFound,

        /// <summary>Not all requested items are on the board.</summary>
        MissingItems,

        /// <summary>The dragged item isn't one this order still needs.</summary>
        NotNeeded,

        Empty,
        Locked,
    }
}
