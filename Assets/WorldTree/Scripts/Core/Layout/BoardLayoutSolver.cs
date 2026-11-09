using System;

namespace WorldTree.Core.Layout
{
    /// <summary>
    /// Portrait layout math from 06-mobile-layout.md §3 (engine-free so it can be unit-tested for every
    /// reference device). Regions top to bottom: HUD bar, optional scene banner, orders strip, board,
    /// context bar. The board is anchored to the top of the context bar, in the thumb zone.
    /// </summary>
    public static class BoardLayoutSolver
    {
        public const float SideMargin = 8f;
        public const float MinCell = 44f;
        public const float MaxCell = 80f;
        public const float MinBanner = 60f;

        // HUD, orders strip, context bar heights per tier (§2).
        private static readonly float[,] Chrome =
        {
            { 56f, 104f, 60f }, // Regular
            { 48f, 76f, 52f },  // Compact
            { 44f, 64f, 48f },  // Minimum
        };

        private static readonly float[] TierMinCell = { 48f, 46f, 0f };

        /// <param name="safeWidth">Safe-area width in points.</param>
        /// <param name="safeHeight">Safe-area height in points.</param>
        public static ScreenLayout Solve(float safeWidth, float safeHeight, int columns, int rows)
        {
            if (columns <= 0 || rows <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
            float widthCell = (safeWidth - 2f * SideMargin) / columns;
            int tier = 0;
            float cell = 0f;
            for (; tier < 3; tier++)
            {
                float chrome = Chrome[tier, 0] + Chrome[tier, 1] + Chrome[tier, 2];
                cell = Math.Min(widthCell, (safeHeight - chrome) / rows);
                if (cell >= TierMinCell[tier]) break;
            }
            if (tier == 3) tier = 2;
            cell = Math.Max(MinCell, Math.Min(MaxCell, cell));

            float hud = Chrome[tier, 0], orders = Chrome[tier, 1], context = Chrome[tier, 2];
            float boardWidth = cell * columns, boardHeight = cell * rows;
            float leftover = Math.Max(0f, safeHeight - hud - orders - context - boardHeight);
            float banner = leftover >= MinBanner ? leftover : 0f;
            float spareAbove = banner > 0f ? 0f : leftover / 2f;
            return new ScreenLayout((LayoutTier)tier, cell, hud, orders, context, boardWidth, boardHeight, banner, spareAbove);
        }
    }
}
