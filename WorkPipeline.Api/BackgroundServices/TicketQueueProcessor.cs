using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkPipeline.Core.Interfaces;
using WorkPipeline.Infrastructure.Services;

namespace WorkPipeline.Api.BackgroundServices;

public class TicketQueueProcessor(
    ITicketQueue queue,
    IServiceProvider serviceProvider,
    ILogger<TicketQueueProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Ticket queue processor started.");
        await foreach (var ticketWorkId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<WorkflowOrchestrator>();
                await orchestrator.ProcessTicketAsync(ticketWorkId, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error processing ticket {Id}", ticketWorkId);
            }
        }
    }
}
