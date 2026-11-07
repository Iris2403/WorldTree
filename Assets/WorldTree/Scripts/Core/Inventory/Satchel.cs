using System;
using System.Collections.Generic;
using WorldTree.Core.Board;

namespace WorldTree.Core.Inventory
{
    /// <summary>
    /// Off-board storage (GDD §3.5). Items keep their own state, so a generator stored here keeps
    /// its charges and its refresh timer keeps running.
    /// </summary>
    public sealed class Satchel
    {
        private readonly List<ItemInstance> _items = new List<ItemInstance>();

        public Satchel(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public int Capacity { get; }
        public int Count => _items.Count;
        public bool IsFull => _items.Count >= Capacity;
        public IReadOnlyList<ItemInstance> Items => _items;

        internal void Add(ItemInstance item) => _items.Add(item);

        /// <summary>Restores saved contents even if they exceed the capacity, so nothing is ever lost.</summary>
        internal void Restore(ItemInstance item) => _items.Add(item);

        internal ItemInstance RemoveAt(int index)
        {
            var item = _items[index];
            _items.RemoveAt(index);
            return item;
        }
    }
}
