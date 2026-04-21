using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkPipeline.Core.DTOs;
using WorkPipeline.Core.Enums;
using WorkPipeline.Core.Interfaces;
using WorkPipeline.Infrastructure.Data;

namespace WorkPipeline.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/workflow")]
public class WorkflowController(
    IJiraService jira,
    ITicketQueue queue,
    AppDbContext db) : ControllerBase
{
    [HttpPost("start")]
    public async Task<IActionResult> StartWorkflow([FromBody] StartWorkflowRequest req)
    {
        if (!req.TicketKeys.Any())
            return BadRequest(new { message = "No tickets selected." });

        var workspace = await db.Workspaces.FindAsync(req.WorkspaceId);
        if (workspace == null)
            return BadRequest(new { message = "Workspace not found." });
        if (!workspace.IsFullyConfigured)
            return BadRequest(new { message = "Workspace is not fully configured." });

        var session = new Core.Entities.WorkSession
        {
            Name = $"Session {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            WorkspaceId = req.WorkspaceId,
            TotalTickets = req.TicketKeys.Count
        };
        db.WorkSessions.Add(session);
        await db.SaveChangesAsync();

        var position = 0;
        foreach (var key in req.TicketKeys)
        {
            var ticketDetail = await jira.GetTicketAsync(
                workspace.JiraSiteUrl!, workspace.JiraCredentials!, key);
            if (ticketDetail == null) continue;

            var work = new Core.Entities.TicketWork
            {
                SessionId = session.Id,
                JiraTicketKey = key,
                JiraTicketTitle = ticketDetail.Title,
                JiraTicketDescription = ticketDetail.Description,
                JiraIssueType = ticketDetail.IssueType,
                Priority = ticketDetail.Priority,
                Assignee = ticketDetail.Assignee,
                QueuePosition = position++,
                Status = TicketWorkStatus.Queued
            };
            db.TicketWorks.Add(work);
            await db.SaveChangesAsync();
            queue.Enqueue(work.Id);
        }

        return Ok(new { sessionId = session.Id });
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await db.WorkSessions
            .Include(s => s.Tickets)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(sessions.Select(MapSession));
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetSession(Guid sessionId)
    {
        var session = await db.WorkSessions
            .Include(s => s.Tickets)
                .ThenInclude(t => t.Transcript)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        return session == null ? NotFound() : Ok(MapSession(session));
    }

    [HttpPost("tickets/{ticketWorkId:guid}/retry")]
    public async Task<IActionResult> RetryTicket(Guid ticketWorkId)
    {
        var ticket = await db.TicketWorks.FindAsync(ticketWorkId);
        if (ticket == null) return NotFound();
        if (ticket.Status != TicketWorkStatus.Failed)
            return BadRequest(new { message = "Only failed tickets can be retried." });

        ticket.Status = TicketWorkStatus.Queued;
        ticket.RetryCount = 0;
        ticket.ErrorMessage = null;
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        queue.Enqueue(ticket.Id);

        return Ok(new { queued = true });
    }

    /// <summary>
    /// For each ticket key, return all sessions that contain it.
    /// </summary>
    [HttpPost("sessions/by-tickets")]
    public async Task<IActionResult> GetSessionsByTicketKeys([FromBody] List<string> ticketKeys)
    {
        if (ticketKeys == null || ticketKeys.Count == 0)
            return Ok(new Dictionary<string, object>());

        var result = new Dictionary<string, object>();

        foreach (var key in ticketKeys.Distinct())
        {
            var sessions = await db.TicketWorks
                .Where(t => t.JiraTicketKey == key)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new { 
                    sessionId = t.SessionId, 
                    status = t.Status.ToString(),
                    createdAt = t.CreatedAt 
                })
                .ToListAsync();

            if (sessions.Any())
                result[key] = sessions;
        }

        return Ok(result);
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> DeleteSession(Guid sessionId)
    {
        var session = await db.WorkSessions.Include(s => s.Tickets)
            .ThenInclude(t => t.Transcript)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null) return NotFound();
        db.WorkSessions.Remove(session);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static WorkSessionDto MapSession(Core.Entities.WorkSession s) => new(
        s.Id, s.Name, s.WorkspaceId, s.TotalTickets, s.ProcessedTickets,
        s.CreatedAt, s.CompletedAt, s.Tickets.Select(MapTicket).ToList()
    );

    private static TicketWorkDto MapTicket(Core.Entities.TicketWork t) => new(
        t.Id, t.SessionId, t.JiraTicketKey, t.JiraTicketTitle, t.JiraTicketDescription,
        t.JiraIssueType, t.Priority, t.Assignee, t.Status, t.BranchName,
        t.PullRequestUrl, t.PullRequestNumber, t.RetryCount, t.ErrorMessage,
        t.QueuePosition, t.CreatedAt, t.UpdatedAt,
        t.Transcript?.Select(tr => new TranscriptEntryDto(
            tr.Id, tr.Role, tr.Content, tr.Sequence, tr.RoundNumber, tr.IsRetryRound, tr.CreatedAt
        )).OrderBy(tr => tr.Sequence).ToList()
    );
}
