namespace WorldTree.Game
{
    /// <summary>Platform haptics behind an interface (architecture §4.2). <see cref="Haptics.Create"/> picks the implementation.</summary>
    public interface IHaptics
    {
        void Play(HapticKind kind);
    }
}
