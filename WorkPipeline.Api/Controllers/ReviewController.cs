using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkPipeline.Core.Enums;
using WorkPipeline.Infrastructure.Data;

namespace WorkPipeline.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/review")]
public class ReviewController(AppDbContext db) : ControllerBase
{
    [HttpGet("board")]
    public async Task<IActionResult> GetBoard()
    {
        var tickets = await db.TicketWorks
            .Where(t => t.Status == TicketWorkStatus.AwaitingReview
                     || t.Status == TicketWorkStatus.ChangesRequested
                     || t.Status == TicketWorkStatus.Merged)
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync();

        var grouped = new
        {
            awaitingReview = tickets.Where(t => t.Status == TicketWorkStatus.AwaitingReview).Select(MapCard),
            changesRequested = tickets.Where(t => t.Status == TicketWorkStatus.ChangesRequested).Select(MapCard),
            merged = tickets.Where(t => t.Status == TicketWorkStatus.Merged).Select(MapCard)
        };
        return Ok(grouped);
    }

    [HttpGet("tickets/{ticketWorkId:guid}/transcript")]
    public async Task<IActionResult> GetTranscript(Guid ticketWorkId)
    {
        var entries = await db.TranscriptEntries
            .Where(e => e.TicketWorkId == ticketWorkId)
            .OrderBy(e => e.Sequence)
            .Select(e => new
            {
                e.Id,
                e.Role,
                e.Content,
                e.Sequence,
                e.RoundNumber,
                e.IsRetryRound,
                e.CreatedAt
            })
            .ToListAsync();
        return Ok(entries);
    }

    private static object MapCard(Core.Entities.TicketWork t) => new
    {
        t.Id,
        t.SessionId,
        t.JiraTicketKey,
        t.JiraTicketTitle,
        t.JiraIssueType,
        t.Priority,
        t.Assignee,
        Status = t.Status.ToString(),
        t.BranchName,
        t.PullRequestUrl,
        t.PullRequestNumber,
        t.UpdatedAt
    };
}
