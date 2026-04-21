namespace WorkPipeline.Core.Entities;

public class WebhookEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Source { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public Guid? TicketWorkId { get; set; }
    public TicketWork? TicketWork { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
