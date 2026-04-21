using WorkPipeline.Core.Enums;

namespace WorkPipeline.Core.DTOs;

public record StartWorkflowRequest(Guid WorkspaceId, List<string> TicketKeys);

public record WorkSessionDto(
    Guid Id,
    string Name,
    Guid WorkspaceId,
    int TotalTickets,
    int ProcessedTickets,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    List<TicketWorkDto> Tickets
);

public record TicketWorkDto(
    Guid Id,
    Guid SessionId,
    string JiraTicketKey,
    string JiraTicketTitle,
    string? JiraTicketDescription,
    string JiraIssueType,
    string Priority,
    string? Assignee,
    TicketWorkStatus Status,
    string? BranchName,
    string? PullRequestUrl,
    int? PullRequestNumber,
    int RetryCount,
    string? ErrorMessage,
    int QueuePosition,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<TranscriptEntryDto>? Transcript = null
);

public record TranscriptEntryDto(
    Guid Id,
    string Role,
    string Content,
    int Sequence,
    int RoundNumber,
    bool IsRetryRound,
    DateTime CreatedAt
);

public record StreamEventDto(
    string Event,
    Guid TicketWorkId,
    string? JiraKey = null,
    string? Status = null,
    string? Chunk = null,
    string? Role = null,
    int? RoundNumber = null,
    bool? TestsPassed = null,
    string? TestOutput = null,
    string? BranchName = null,
    string? PrUrl = null,
    int? PrNumber = null,
    string? NewJiraStatus = null,
    string? ErrorMessage = null,
    int? RetryCount = null,
    string[]? ConflictingFiles = null
);
