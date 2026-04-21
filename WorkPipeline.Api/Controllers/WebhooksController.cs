using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WorkPipeline.Core.Entities;
using WorkPipeline.Core.Enums;
using WorkPipeline.Core.Interfaces;
using WorkPipeline.Infrastructure.Data;
using WorkPipeline.Infrastructure.Hubs;

namespace WorkPipeline.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController(
    AppDbContext db,
    IGitHubService github,
    IJiraService jira,
    IHubContext<WorkflowHub> hub) : ControllerBase
{
    [HttpPost("github")]
    public async Task<IActionResult> GitHubWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();

        var signature = Request.Headers["X-Hub-Signature-256"].ToString();
        if (!github.VerifyWebhookSignature(payload, signature))
            return Unauthorized();

        var eventType = Request.Headers["X-GitHub-Event"].ToString();
        var webhookEvent = new WebhookEvent
        {
            Source = "github",
            EventType = eventType,
            Payload = payload
        };

        if (eventType == "pull_request")
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var action = root.GetProperty("action").GetString();
            var merged = root.TryGetProperty("pull_request", out var pr) &&
                         pr.TryGetProperty("merged", out var m) && m.GetBoolean();

            webhookEvent.Action = action ?? string.Empty;

            if (action == "closed" && merged)
            {
                var prNumber = pr.GetProperty("number").GetInt32();
                var ticket = await db.TicketWorks
                    .Include(t => t.Session)
                    .ThenInclude(s => s.Workspace)
                    .FirstOrDefaultAsync(t => t.PullRequestNumber == prNumber);

                if (ticket != null)
                {
                    webhookEvent.TicketWorkId = ticket.Id;
                    ticket.Status = TicketWorkStatus.Merged;
                    ticket.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();

                    var ws = ticket.Session.Workspace;
                    if (ws.JiraSiteUrl != null && ws.JiraCredentials != null)
                    {
                        var mergedStatus = ws.JiraMergedStatus ?? "Done";
                        await jira.TransitionTicketAsync(ws.JiraSiteUrl, ws.JiraCredentials, ticket.JiraTicketKey, mergedStatus);
                        ticket.JiraTransitionedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }

                    await hub.Clients.Group($"session:{ticket.SessionId}").SendAsync("JiraUpdated",
                        new { ticketWorkId = ticket.Id, newJiraStatus = ws.JiraMergedStatus ?? "Done" });
                    await hub.Clients.Group($"session:{ticket.SessionId}").SendAsync("TicketStatusChanged",
                        new { ticketWorkId = ticket.Id, jiraKey = ticket.JiraTicketKey, status = "Merged" });
                }
            }
            else if (action == "review_requested" || action == "changes_requested")
            {
                var prNumber = pr.GetProperty("number").GetInt32();
                var ticket = await db.TicketWorks.FirstOrDefaultAsync(t => t.PullRequestNumber == prNumber);
                if (ticket != null)
                {
                    webhookEvent.TicketWorkId = ticket.Id;
                    ticket.Status = TicketWorkStatus.ChangesRequested;
                    ticket.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    await hub.Clients.Group($"session:{ticket.SessionId}").SendAsync("TicketStatusChanged",
                        new { ticketWorkId = ticket.Id, jiraKey = ticket.JiraTicketKey, status = "ChangesRequested" });
                }
            }
        }

        db.WebhookEvents.Add(webhookEvent);
        webhookEvent.ProcessedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok();
    }
}
