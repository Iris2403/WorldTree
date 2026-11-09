namespace WorldTree.Core.Layout
{
    /// <summary>Result of <see cref="BoardLayoutSolver.Solve"/>. All sizes in points (iOS) / dp (Android).</summary>
    public readonly struct ScreenLayout
    {
        public ScreenLayout(LayoutTier tier, float cell, float hudHeight, float ordersHeight, float contextHeight, float boardWidth,
            float boardHeight, float bannerHeight, float spareAbove)
        {
            Tier = tier;
            Cell = cell;
            HudHeight = hudHeight;
            OrdersHeight = ordersHeight;
            ContextHeight = contextHeight;
            BoardWidth = boardWidth;
            BoardHeight = boardHeight;
            BannerHeight = bannerHeight;
            SpareAbove = spareAbove;
        }

        public LayoutTier Tier { get; }
        public float Cell { get; }
        public float HudHeight { get; }
        public float OrdersHeight { get; }
        public float ContextHeight { get; }
        public float BoardWidth { get; }
        public float BoardHeight { get; }

        /// <summary>Height of the scene banner between HUD and orders, or 0 when there is less than 60 pt to spare.</summary>
        public float BannerHeight { get; }

        /// <summary>Leftover height that isn't used by a banner, split evenly above and below the orders strip.</summary>
        public float SpareAbove { get; }
    }
}
