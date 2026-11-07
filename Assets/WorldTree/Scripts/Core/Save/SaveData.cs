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
    }
}
