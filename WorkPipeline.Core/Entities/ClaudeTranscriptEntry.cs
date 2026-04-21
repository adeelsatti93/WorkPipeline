namespace WorkPipeline.Core.Entities;

public class ClaudeTranscriptEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketWorkId { get; set; }
    public TicketWork TicketWork { get; set; } = null!;

    public string Role { get; set; } = string.Empty; // "user" | "assistant" | "system"
    public string Content { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public int RoundNumber { get; set; } = 1;
    public bool IsRetryRound { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
