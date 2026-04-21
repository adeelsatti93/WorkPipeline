using System.Runtime.CompilerServices;
using System.Threading.Channels;
using WorkPipeline.Core.Interfaces;

namespace WorkPipeline.Infrastructure.Services;

public class TicketQueue : ITicketQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid ticketWorkId) =>
        _channel.Writer.TryWrite(ticketWorkId);

    public async IAsyncEnumerable<Guid> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var id in _channel.Reader.ReadAllAsync(cancellationToken))
            yield return id;
    }
}
