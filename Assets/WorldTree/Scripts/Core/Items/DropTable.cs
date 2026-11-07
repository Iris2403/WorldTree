using System;
using System.Collections.Generic;
using WorldTree.Core.Common;

namespace WorldTree.Core.Items
{
    /// <summary>Weighted list of items a generator can produce (GDD §4.1).</summary>
    public sealed class DropTable
    {
        private readonly DropEntry[] _entries;

        public DropTable(IReadOnlyList<DropEntry> entries)
        {
            if (entries == null || entries.Count == 0) throw new ArgumentException("A drop table needs at least one entry.", nameof(entries));
            _entries = new DropEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                _entries[i] = entries[i];
                TotalWeight += entries[i].Weight;
            }
        }

        public IReadOnlyList<DropEntry> Entries => _entries;
        public int TotalWeight { get; }

        public string Roll(IRandom random)
        {
            int roll = random.Next(TotalWeight);
            for (int i = 0; i < _entries.Length; i++)
            {
                roll -= _entries[i].Weight;
                if (roll < 0) return _entries[i].ItemId;
            }
            return _entries[_entries.Length - 1].ItemId; // unreachable with a valid random source
        }
    }
}
