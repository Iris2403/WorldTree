using System;
using System.Collections.Generic;

namespace WorldTree.Core.Content
{
    /// <summary>An authored starting board (GDD §3.1). Cells not listed start Open and empty.</summary>
    public sealed class BoardLayout
    {
        private readonly LayoutPlacement[] _placements;

        public BoardLayout(int width, int height, IReadOnlyList<LayoutPlacement> placements)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            _placements = new LayoutPlacement[placements?.Count ?? 0];
            for (int i = 0; i < _placements.Length; i++) _placements[i] = placements[i];
        }

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<LayoutPlacement> Placements => _placements;
    }
}
