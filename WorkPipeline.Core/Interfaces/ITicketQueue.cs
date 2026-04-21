namespace WorkPipeline.Core.Interfaces;

public interface ITicketQueue
{
    void Enqueue(Guid ticketWorkId);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}
