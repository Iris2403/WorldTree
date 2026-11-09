namespace WorldTree.Core.Settings
{
    /// <summary>Audio, haptics and accessibility options (GDD §10), saved with the game.</summary>
    public sealed class GameSettings
    {
        public bool Music { get; internal set; } = true;
        public bool Sound { get; internal set; } = true;
        public bool Haptics { get; internal set; } = true;

        /// <summary>Shorter, calmer animations and fewer particles (GDD §10 accessibility).</summary>
        public bool ReducedMotion { get; internal set; }

        /// <summary>Larger dialogue and info text (GDD §10 accessibility).</summary>
        public bool LargeText { get; internal set; }
    }
}
