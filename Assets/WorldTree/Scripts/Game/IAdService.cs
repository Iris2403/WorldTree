using System;

namespace WorldTree.Game
{
    /// <summary>
    /// Rewarded ads (GDD §11). Optional and never shown automatically. The real implementation arrives
    /// in Phase 8; until then <see cref="NoAdService"/> keeps every ad button hidden.
    /// </summary>
    public interface IAdService
    {
        bool IsRewardedReady { get; }

        /// <summary>Shows a rewarded ad. <paramref name="onFinished"/> receives true only if it was watched to the end.</summary>
        void ShowRewarded(Action<bool> onFinished);
    }
}
