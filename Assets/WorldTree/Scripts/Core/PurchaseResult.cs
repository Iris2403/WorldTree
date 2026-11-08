namespace WorldTree.Core
{
    /// <summary>Outcome of spending a currency or using a capped bonus.</summary>
    public enum PurchaseResult
    {
        Done,
        NotEnoughMoonpetals,
        DailyLimitReached,
        NotApplicable,
    }
}
