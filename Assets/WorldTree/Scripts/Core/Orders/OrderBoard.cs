using System;
using System.Collections.Generic;

namespace WorldTree.Core.Orders
{
    /// <summary>The active orders and the order history the generator needs (GDD §6.1).</summary>
    public sealed class OrderBoard
    {
        private readonly List<Order> _active = new List<Order>();
        private readonly List<string> _recentChains = new List<string>();
        private readonly List<string> _windowChains = new List<string>();
        private readonly List<int> _windowValues = new List<int>();

        public OrderBoard(int nextId, int completed, DateTime? lastSwapUtc)
        {
            NextId = Math.Max(1, nextId);
            Completed = Math.Max(0, completed);
            LastSwapUtc = lastSwapUtc;
        }

        public IReadOnlyList<Order> Active => _active;
        public int NextId { get; private set; }
        public int Completed { get; private set; }
        public DateTime? LastSwapUtc { get; internal set; }

        /// <summary>Chains of the most recent orders, newest last (kept only as long as the demand rule needs).</summary>
        public IReadOnlyList<string> RecentChains => _recentChains;

        /// <summary>Chain and value of the last <see cref="OrderRules.DemandWindow"/> orders, oldest first.</summary>
        public IReadOnlyList<string> WindowChains => _windowChains;
        public IReadOnlyList<int> WindowValues => _windowValues;

        public int RecentTotal
        {
            get
            {
                int sum = 0;
                foreach (var v in _windowValues) sum += v;
                return sum;
            }
        }

        /// <summary>Demand of <paramref name="chainId"/> among the last <see cref="OrderRules.DemandWindow"/> orders.</summary>
        public int RecentDemand(string chainId)
        {
            int sum = 0;
            for (int i = 0; i < _windowChains.Count; i++) if (_windowChains[i] == chainId) sum += _windowValues[i];
            return sum;
        }

        public Order Find(int id)
        {
            foreach (var o in _active) if (o.Id == id) return o;
            return null;
        }

        internal int TakeId() => NextId++;

        internal void Add(Order order, int value)
        {
            _active.Add(order);
            _recentChains.Add(order.ChainId);
            while (_recentChains.Count > OrderRules.MaxConsecutiveSameChain) _recentChains.RemoveAt(0);
            RestoreWindowEntry(order.ChainId, value);
        }

        internal void RestoreWindowEntry(string chainId, int value)
        {
            _windowChains.Add(chainId);
            _windowValues.Add(value);
            while (_windowChains.Count > OrderRules.DemandWindow)
            {
                _windowChains.RemoveAt(0);
                _windowValues.RemoveAt(0);
            }
        }

        /// <summary>Restores saved state without touching the history twice.</summary>
        internal void Restore(Order order) => _active.Add(order);

        internal void RestoreRecentChain(string chainId)
        {
            _recentChains.Add(chainId);
            while (_recentChains.Count > OrderRules.MaxConsecutiveSameChain) _recentChains.RemoveAt(0);
        }

        internal void Remove(Order order) => _active.Remove(order);
        internal void CountCompleted() => Completed++;
    }
}
