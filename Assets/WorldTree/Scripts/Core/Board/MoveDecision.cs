namespace WorldTree.Core.Board
{
    /// <summary>What a drag will do, decided by <see cref="MergeRules.Resolve"/> before any state changes.</summary>
    public readonly struct MoveDecision
    {
        public MoveDecision(MoveKind kind, RejectReason reason, string resultItemId)
        {
            Kind = kind;
            Reason = reason;
            ResultItemId = resultItemId;
        }

        public MoveKind Kind { get; }
        public RejectReason Reason { get; }

        /// <summary>For merges: the item that will appear in the target cell.</summary>
        public string ResultItemId { get; }

        public static MoveDecision Of(MoveKind kind) => new MoveDecision(kind, RejectReason.None, null);
        public static MoveDecision Reject(RejectReason reason) => new MoveDecision(MoveKind.Rejected, reason, null);
        public static MoveDecision MergeInto(string resultItemId) => new MoveDecision(MoveKind.Merge, RejectReason.None, resultItemId);
    }
}
