namespace WorldTree.Core.Board
{
    /// <summary>What a drag will do, decided by <see cref="MergeRules.Resolve"/> before any state changes.</summary>
    public readonly struct MoveDecision
    {
        public MoveDecision(MoveKind kind, RejectReason reason, string resultItemId, bool cleansingTarget = false)
        {
            Kind = kind;
            Reason = reason;
            ResultItemId = resultItemId;
            CleansingTarget = cleansingTarget;
        }

        public MoveKind Kind { get; }
        public RejectReason Reason { get; }

        /// <summary>For merges: the item that will appear in the target cell.</summary>
        public string ResultItemId { get; }

        /// <summary>True for a cleansing merge: the target was a Withered item and its cell becomes Open (GDD §3.2).</summary>
        public bool CleansingTarget { get; }

        public static MoveDecision Of(MoveKind kind) => new MoveDecision(kind, RejectReason.None, null);
        public static MoveDecision Reject(RejectReason reason) => new MoveDecision(MoveKind.Rejected, reason, null);
        public static MoveDecision MergeInto(string resultItemId) => new MoveDecision(MoveKind.Merge, RejectReason.None, resultItemId);
        public static MoveDecision CleanseInto(string resultItemId) => new MoveDecision(MoveKind.Merge, RejectReason.None, resultItemId, true);
    }
}
