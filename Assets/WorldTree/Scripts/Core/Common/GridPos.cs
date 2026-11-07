using System;

namespace WorldTree.Core.Common
{
    /// <summary>
    /// A board cell coordinate. (0, 0) is the top-left cell; X grows to the right, Y grows downward
    /// (GDD §3.1). Rows are therefore "higher on screen" when Y is smaller.
    /// </summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int ManhattanDistanceTo(GridPos other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);
        public override string ToString() => "(" + X + ", " + Y + ")";
    }
}
