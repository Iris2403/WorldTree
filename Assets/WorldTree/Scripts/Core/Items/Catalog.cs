using System;
using System.Collections.Generic;

namespace WorldTree.Core.Items
{
    /// <summary>
    /// Immutable set of chains with validated cross-references. Construction fails fast on duplicate
    /// IDs or on drop tables that reference unknown items, so bad data never reaches gameplay.
    /// </summary>
    public sealed class Catalog : ICatalog
    {
        private readonly Dictionary<string, ItemDef> _items = new Dictionary<string, ItemDef>(StringComparer.Ordinal);
        private readonly ChainDef[] _chains;

        public Catalog(IEnumerable<ChainDef> chains)
        {
            if (chains == null) throw new ArgumentNullException(nameof(chains));
            var list = new List<ChainDef>();
            var chainIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var chain in chains)
            {
                if (!chainIds.Add(chain.Id)) throw new ArgumentException("Duplicate chain ID: " + chain.Id);
                foreach (var item in chain.Levels)
                {
                    if (_items.ContainsKey(item.Id)) throw new ArgumentException("Duplicate item ID: " + item.Id);
                    _items.Add(item.Id, item);
                }
                list.Add(chain);
            }
            _chains = list.ToArray();

            foreach (var item in _items.Values)
            {
                if (!item.IsGenerator) continue;
                foreach (var drop in item.Generator.Drops.Entries)
                {
                    if (!_items.ContainsKey(drop.ItemId))
                        throw new ArgumentException("Generator " + item.Id + " drops unknown item " + drop.ItemId);
                }
            }
        }

        public IReadOnlyList<ChainDef> Chains => _chains;

        public bool TryGetItem(string id, out ItemDef item)
        {
            if (id == null)
            {
                item = null;
                return false;
            }
            return _items.TryGetValue(id, out item);
        }

        public ItemDef GetItem(string id)
        {
            if (!TryGetItem(id, out var item)) throw new KeyNotFoundException("Unknown item ID: " + id);
            return item;
        }
    }
}
