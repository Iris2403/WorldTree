using System;
using System.Collections.Generic;
using WorldTree.Core.Common;

namespace WorldTree.Core.Items
{
    /// <summary>An ordered chain of item levels. Two items of level N merge into level N + 1.</summary>
    public sealed class ChainDef
    {
        private ChainDef(string id, ItemDef[] levels)
        {
            Id = id;
            Levels = levels;
        }

        public string Id { get; }
        public IReadOnlyList<ItemDef> Levels { get; }

        public static ChainDef Create(string chainId, IReadOnlyList<LevelSpec> levels)
        {
            Ids.Require(chainId, nameof(chainId));
            if (levels == null || levels.Count == 0) throw new ArgumentException("A chain needs at least one level.", nameof(levels));
            var defs = new ItemDef[levels.Count];
            for (int i = 0; i < levels.Count; i++)
            {
                string next = i + 1 < levels.Count ? levels[i + 1].Id : null;
                defs[i] = new ItemDef(levels[i].Id, chainId, i + 1, next, levels[i].SellValue, levels[i].Generator);
            }
            return new ChainDef(chainId, defs);
        }
    }
}
