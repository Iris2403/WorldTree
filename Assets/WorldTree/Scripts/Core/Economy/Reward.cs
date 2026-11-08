using System;
using WorldTree.Core.Common;

namespace WorldTree.Core.Economy
{
    /// <summary>One reward: an amount of a currency, or an item.</summary>
    public readonly struct Reward
    {
        private Reward(RewardKind kind, int amount, string itemId)
        {
            Kind = kind;
            Amount = amount;
            ItemId = itemId;
        }

        public RewardKind Kind { get; }
        public int Amount { get; }
        public string ItemId { get; }

        public static Reward Sap(int amount) => new Reward(RewardKind.Sap, Positive(amount), null);
        public static Reward Glimmer(int amount) => new Reward(RewardKind.Glimmer, Positive(amount), null);
        public static Reward Moonpetals(int amount) => new Reward(RewardKind.Moonpetals, Positive(amount), null);

        public static Reward Item(string itemId)
        {
            Ids.Require(itemId, nameof(itemId));
            return new Reward(RewardKind.Item, 1, itemId);
        }

        private static int Positive(int amount) => amount > 0 ? amount : throw new ArgumentOutOfRangeException(nameof(amount));
    }
}
