using System;

namespace WorldTree.Game
{
    /// <summary>Default until Phase 8: no ads, so ad buttons stay hidden.</summary>
    public sealed class NoAdService : IAdService
    {
        public bool IsRewardedReady => false;
        public void ShowRewarded(Action<bool> onFinished) => onFinished(false);
    }
}
