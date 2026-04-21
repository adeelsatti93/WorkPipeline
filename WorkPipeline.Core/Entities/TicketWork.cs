using WorkPipeline.Core.Enums;

namespace WorkPipeline.Core.Entities;

public class TicketWork
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public WorkSession Session { get; set; } = null!;

    public string JiraTicketKey { get; set; } = string.Empty;
    public string JiraTicketTitle { get; set; } = string.Empty;
    public string? JiraTicketDescription { get; set; }
    public string JiraIssueType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? Assignee { get; set; }

    public TicketWorkStatus Status { get; set; } = TicketWorkStatus.Queued;
    public string? BranchName { get; set; }
    public string? PullRequestUrl { get; set; }
    public int? PullRequestNumber { get; set; }
    public DateTime? JiraTransitionedAt { get; set; }

    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public int QueuePosition { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ClaudeTranscriptEntry> Transcript { get; set; } = new List<ClaudeTranscriptEntry>();
}
