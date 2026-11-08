namespace WorldTree.Core.Orders
{
    /// <summary>One requested item. Becomes fulfilled when the player drags a matching item onto the order.</summary>
    public sealed class OrderRequirement
    {
        public OrderRequirement(string itemId, bool fulfilled = false)
        {
            ItemId = itemId;
            Fulfilled = fulfilled;
        }

        public string ItemId { get; }
        public bool Fulfilled { get; internal set; }
    }
}
