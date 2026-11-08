using System;
using System.Collections.Generic;

namespace WorldTree.Core.Save
{
    /// <summary>
    /// Serializable snapshot of the game state (architecture §6.2). Plain fields only; timestamps are
    /// UTC ticks; IDs are stable strings. Only non-default cells are stored.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public int Width;
        public int Height;
        public List<CellData> Cells = new List<CellData>();
        public int Sap;
        public long? SapNextTickUtcTicks;
        public uint RandomState;

        // Phase 2. Absent in Phase 1 saves, which then load with the defaults below.
        public int Glimmer;
        public List<SatchelItemData> Satchel = new List<SatchelItemData>();
        public List<string> Discovered = new List<string>();

        // Phase 3. Absent in older saves, which then load with these defaults.
        public int Moonpetals;
        public int DailyGiftNextDay;
        public int? DailyGiftLastClaimDay;
        public int? LimitsDay;
        public int SapRefillsToday;
        public int AdSapClaimsToday;

        // Phase 4. Absent in older saves: level 1, no orders (new ones are generated on load).
        public int Lifebloom;
        public int Level = 1;
        public int Xp;
        public List<OrderData> Orders = new List<OrderData>();
        public int NextOrderId = 1;
        public int OrdersCompleted;
        public long? LastOrderSwapUtcTicks;
        public List<string> RecentOrderChains = new List<string>();
        public List<string> DemandWindowChains = new List<string>();
        public List<int> DemandWindowValues = new List<int>();
    }
}
