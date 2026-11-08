using System.Collections.Generic;

namespace WorldTree.Core.Orders
{
    /// <summary>A customer order (GDD §6.1): 1–3 requested items and the rewards for completing it.</summary>
    public sealed class Order
    {
        public Order(int id, string customerId, string chainId, IReadOnlyList<OrderRequirement> requirements,
            int glimmer, int xp, int lifebloom, string bonusItemId)
        {
            Id = id;
            CustomerId = customerId;
            ChainId = chainId;
            Requirements = requirements;
            Glimmer = glimmer;
            Xp = xp;
            Lifebloom = lifebloom;
            BonusItemId = bonusItemId;
        }

        public int Id { get; }
        public string CustomerId { get; }

        /// <summary>The chain this order draws from (used by the demand rule).</summary>
        public string ChainId { get; }

        public IReadOnlyList<OrderRequirement> Requirements { get; }
        public int Glimmer { get; }
        public int Xp { get; }
        public int Lifebloom { get; }

        /// <summary>Optional extra item reward (for example a Mystery Acorn), or null.</summary>
        public string BonusItemId { get; }

        public bool HasProgress
        {
            get
            {
                foreach (var r in Requirements) if (r.Fulfilled) return true;
                return false;
            }
        }

        public bool IsComplete
        {
            get
            {
                foreach (var r in Requirements) if (!r.Fulfilled) return false;
                return true;
            }
        }
    }
}
