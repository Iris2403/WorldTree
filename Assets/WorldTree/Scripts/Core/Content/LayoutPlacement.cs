using WorldTree.Core.Board;
using WorldTree.Core.Common;

namespace WorldTree.Core.Content
{
    /// <summary>One authored cell of a starting board.</summary>
    public readonly struct LayoutPlacement
    {
        public LayoutPlacement(GridPos pos, string itemId, CellState state = CellState.Open)
        {
            Pos = pos;
            ItemId = itemId;
            State = state;
        }

        public GridPos Pos { get; }
        public string ItemId { get; }
        public CellState State { get; }
    }
}
