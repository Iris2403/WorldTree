using System;
using WorldTree.Core.Common;
using WorldTree.Core.Generators;

namespace WorldTree.Core.Board
{
    /// <summary>An item placed on the board. Generators carry their own <see cref="GeneratorState"/>.</summary>
    public sealed class ItemInstance
    {
        public ItemInstance(string itemId, GeneratorState generator = null)
        {
            Ids.Require(itemId, nameof(itemId));
            ItemId = itemId;
            Generator = generator;
        }

        public string ItemId { get; }

        /// <summary>Non-null for generator items.</summary>
        public GeneratorState Generator { get; }
    }
}
