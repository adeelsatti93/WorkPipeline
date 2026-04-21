namespace WorkPipeline.Core.Entities;

public class WorkSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid WorkspaceId { get; set; }
    public int TotalTickets { get; set; }
    public int ProcessedTickets { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public ICollection<TicketWork> Tickets { get; set; } = new List<TicketWork>();
}
