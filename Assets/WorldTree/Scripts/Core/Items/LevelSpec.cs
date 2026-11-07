using System;
using WorldTree.Core.Common;

namespace WorldTree.Core.Items
{
    /// <summary>Authoring input for one level of a chain.</summary>
    public sealed class LevelSpec
    {
        public LevelSpec(string id, int sellValue, GeneratorDef generator = null)
        {
            Ids.Require(id, nameof(id));
            if (sellValue < 0) throw new ArgumentOutOfRangeException(nameof(sellValue));
            Id = id;
            SellValue = sellValue;
            Generator = generator;
        }

        public string Id { get; }
        public int SellValue { get; }
        public GeneratorDef Generator { get; }
    }
}
