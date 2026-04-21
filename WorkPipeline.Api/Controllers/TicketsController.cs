using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkPipeline.Core.Interfaces;
using WorkPipeline.Infrastructure.Data;

namespace WorkPipeline.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/tickets")]
public class TicketsController(IJiraService jira, AppDbContext db) : ControllerBase
{
    [HttpGet("todo")]
    public async Task<IActionResult> GetTodoTickets([FromQuery] Guid workspaceId, [FromQuery] int maxResults = 100)
    {
        var workspace = await db.Workspaces.FindAsync(workspaceId);
        if (workspace?.JiraCredentials == null || workspace.JiraSiteUrl == null || workspace.JiraBoardId == null)
            return BadRequest(new { message = "Workspace Jira not configured." });

        var tickets = await jira.GetTodoTicketsAsync(
            workspace.JiraSiteUrl, workspace.JiraCredentials, workspace.JiraBoardId, maxResults);
        return Ok(tickets);
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> GetTicket(string key, [FromQuery] Guid workspaceId)
    {
        var workspace = await db.Workspaces.FindAsync(workspaceId);
        if (workspace?.JiraCredentials == null || workspace.JiraSiteUrl == null)
            return BadRequest(new { message = "Workspace Jira not configured." });

        var ticket = await jira.GetTicketAsync(workspace.JiraSiteUrl, workspace.JiraCredentials, key);
        return ticket == null ? NotFound() : Ok(ticket);
    }
}
