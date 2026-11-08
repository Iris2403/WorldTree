using System;

namespace WorldTree.Game
{
    /// <summary>Debug only (toggled in the Debug overlay): every "ad" completes instantly, to test ad rewards.</summary>
    public sealed class FakeAdService : IAdService
    {
        public bool IsRewardedReady => true;
        public void ShowRewarded(Action<bool> onFinished) => onFinished(true);
    }
}
