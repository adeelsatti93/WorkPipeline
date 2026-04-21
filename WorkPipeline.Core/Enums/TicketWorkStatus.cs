namespace WorkPipeline.Core.Enums;

public enum TicketWorkStatus
{
    Queued,
    InProgress,
    VerifyingTests,
    PushingBranch,
    CreatingPR,
    AwaitingReview,
    ChangesRequested,
    Merged,
    Failed,
    Skipped
}
