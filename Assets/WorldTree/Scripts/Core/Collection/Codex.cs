using System;
using System.Collections.Generic;

namespace WorldTree.Core.Collection
{
    /// <summary>The Grove Codex (GDD §3.4): which items the player has discovered.</summary>
    public sealed class Codex
    {
        private readonly HashSet<string> _discovered = new HashSet<string>(StringComparer.Ordinal);

        public int Count => _discovered.Count;
        public IEnumerable<string> Discovered => _discovered;

        public bool IsDiscovered(string itemId) => _discovered.Contains(itemId);

        /// <summary>Marks an item discovered. Returns true only the first time.</summary>
        public bool Discover(string itemId) => _discovered.Add(itemId);
    }
}
