namespace WorldTree.Core.Items
{
    /// <summary>Immutable definition of one item level within a chain. Built by <see cref="ChainDef.Create"/>.</summary>
    public sealed class ItemDef
    {
        internal ItemDef(string id, string chainId, int level, string nextId, int sellValue, GeneratorDef generator)
        {
            Id = id;
            ChainId = chainId;
            Level = level;
            NextId = nextId;
            SellValue = sellValue;
            Generator = generator;
        }

        public string Id { get; }
        public string ChainId { get; }

        /// <summary>1-based level within the chain.</summary>
        public int Level { get; }

        /// <summary>The item two of these merge into, or null at the chain's max level.</summary>
        public string NextId { get; }

        public int SellValue { get; }

        /// <summary>Non-null when this item is a permanent generator.</summary>
        public GeneratorDef Generator { get; }

        public bool IsGenerator => Generator != null;
        public bool IsMaxLevel => NextId == null;
    }
}
