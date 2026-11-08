using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using WorldTree.Core.Board;

namespace WorldTree.Core.Save
{
    /// <summary>One stored cell. The cell state is written as a name, never as an enum number.</summary>
    [Serializable]
    public sealed class CellData
    {
        public int X;
        public int Y;

        [JsonConverter(typeof(StringEnumConverter))]
        public CellState State;

        public string ItemId;
        public int? Charges;
        public long? RefreshEndsUtcTicks;
        public bool? AdSpeedUpUsed;
    }
}
