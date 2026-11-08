using System;
using System.Collections.Generic;

namespace WorldTree.Core.Save
{
    /// <summary>One saved order, with its requirements in order.</summary>
    [Serializable]
    public sealed class OrderData
    {
        public int Id;
        public string CustomerId;
        public string ChainId;
        public List<string> Items = new List<string>();
        public List<bool> Fulfilled = new List<bool>();
        public int Glimmer;
        public int Xp;
        public int Lifebloom;
        public string BonusItemId;
    }
}
