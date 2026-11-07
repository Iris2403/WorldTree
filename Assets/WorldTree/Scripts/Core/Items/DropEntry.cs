using System;
using WorldTree.Core.Common;

namespace WorldTree.Core.Items
{
    /// <summary>One weighted entry of a generator's drop table.</summary>
    public readonly struct DropEntry
    {
        public readonly string ItemId;
        public readonly int Weight;

        public DropEntry(string itemId, int weight)
        {
            Ids.Require(itemId, nameof(itemId));
            if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight), "Drop weights must be positive.");
            ItemId = itemId;
            Weight = weight;
        }
    }
}
