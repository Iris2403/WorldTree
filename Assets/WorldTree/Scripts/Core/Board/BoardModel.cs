using System;
using WorldTree.Core.Common;

namespace WorldTree.Core.Board
{
    /// <summary>
    /// The grid of cells. Holds state only; the rules for moving and merging live in
    /// <see cref="MergeRules"/> and <see cref="GameSession"/>.
    /// </summary>
    public sealed class BoardModel
    {
        private readonly CellState[] _states;
        private readonly ItemInstance[] _items;

        public BoardModel(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            _states = new CellState[width * height];
            _items = new ItemInstance[width * height];
        }

        public int Width { get; }
        public int Height { get; }

        public bool IsInside(GridPos pos) => pos.X >= 0 && pos.Y >= 0 && pos.X < Width && pos.Y < Height;

        public CellState GetState(GridPos pos) => _states[Index(pos)];
        public void SetState(GridPos pos, CellState state) => _states[Index(pos)] = state;

        public ItemInstance GetItem(GridPos pos) => _items[Index(pos)];
        public void SetItem(GridPos pos, ItemInstance item) => _items[Index(pos)] = item;

        public bool IsEmptyOpen(GridPos pos) => GetState(pos) == CellState.Open && GetItem(pos) == null;

        /// <summary>
        /// Nearest empty Open cell by Manhattan distance from <paramref name="origin"/>; ties go to the
        /// smaller row (higher on screen), then the smaller column (GDD §4.1).
        /// </summary>
        public bool TryFindNearestEmptyOpen(GridPos origin, out GridPos result)
        {
            result = default;
            int bestDistance = int.MaxValue;
            // Row-major iteration visits ties in (row, column) order, so the first best wins.
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var pos = new GridPos(x, y);
                    if (!IsEmptyOpen(pos)) continue;
                    int d = pos.ManhattanDistanceTo(origin);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        result = pos;
                    }
                }
            }
            return bestDistance != int.MaxValue;
        }

        private int Index(GridPos pos)
        {
            if (!IsInside(pos)) throw new ArgumentOutOfRangeException(nameof(pos), "Outside the board: " + pos);
            return pos.Y * Width + pos.X;
        }
    }
}
