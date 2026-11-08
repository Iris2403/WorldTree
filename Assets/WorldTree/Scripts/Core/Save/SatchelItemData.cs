using System;

namespace WorldTree.Core.Save
{
    /// <summary>One stored satchel item, in satchel order.</summary>
    [Serializable]
    public sealed class SatchelItemData
    {
        public string ItemId;
        public int? Charges;
        public long? RefreshEndsUtcTicks;
        public bool? AdSpeedUpUsed;
    }
}
