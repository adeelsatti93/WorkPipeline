using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkPipeline.Core.Enums;
using WorkPipeline.Core.Interfaces;
using WorkPipeline.Infrastructure.Data;
using WorkPipeline.Infrastructure.Hubs;

namespace WorkPipeline.Api.BackgroundServices;

public class PrStatusPollingService(
    IServiceProvider serviceProvider,
    ILogger<PrStatusPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PR status polling service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

            try
            {
                await PollAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during PR status polling.");
            }
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var github = scope.ServiceProvider.GetRequiredService<IGitHubService>();
        var jira = scope.ServiceProvider.GetRequiredService<IJiraService>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<WorkflowHub>>();

        var tickets = await db.TicketWorks
            .Include(t => t.Session).ThenInclude(s => s.Workspace)
            .Where(t =>
                (t.Status == TicketWorkStatus.AwaitingReview || t.Status == TicketWorkStatus.ChangesRequested)
                && t.PullRequestNumber != null)
            .ToListAsync(ct);

        if (tickets.Count == 0) return;

        logger.LogInformation("Polling PR status for {Count} ticket(s).", tickets.Count);

        foreach (var ticket in tickets)
        {
            try
            {
                var ws = ticket.Session.Workspace;
                if (ws.GitHubToken == null || ws.GitHubRepoFullName == null) continue;

                var parts = ws.GitHubRepoFullName.Split('/');
                if (parts.Length != 2) continue;

                var pr = await github.GetPullRequestAsync(ws.GitHubToken, parts[0], parts[1], ticket.PullRequestNumber!.Value);
                if (pr == null) continue;

                if (pr.Merged)
                {
                    ticket.Status = TicketWorkStatus.Merged;
                    ticket.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);

                    if (ws.JiraSiteUrl != null && ws.JiraCredentials != null)
                    {
                        var mergedStatus = ws.JiraMergedStatus ?? "Done";
                        await jira.TransitionTicketAsync(ws.JiraSiteUrl, ws.JiraCredentials, ticket.JiraTicketKey, mergedStatus);
                        ticket.JiraTransitionedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(ct);
                    }

                    await hub.Clients.Group($"session:{ticket.SessionId}").SendAsync(
                        "TicketStatusChanged",
                        new { ticketWorkId = ticket.Id, jiraKey = ticket.JiraTicketKey, status = "Merged" },
                        ct);

                    logger.LogInformation("Ticket {Key} PR #{Pr} detected as merged via polling.", ticket.JiraTicketKey, ticket.PullRequestNumber);
                }
                else if (pr.State == "closed")
                {
                    // PR closed without merge — put back to awaiting or leave, log it
                    logger.LogWarning("Ticket {Key} PR #{Pr} was closed without merging.", ticket.JiraTicketKey, ticket.PullRequestNumber);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to poll PR status for ticket {Key}.", ticket.JiraTicketKey);
            }
        }
    }
}
