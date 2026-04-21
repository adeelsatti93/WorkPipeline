using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkPipeline.Infrastructure.Data;

namespace WorkPipeline.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/export")]
public class ExportController(AppDbContext db) : ControllerBase
{
    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<IActionResult> ExportSession(Guid sessionId, [FromQuery] string format = "json")
    {
        var session = await db.WorkSessions
            .Include(s => s.Tickets)
            .ThenInclude(t => t.Transcript)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null) return NotFound();

        if (format.ToLower() == "csv")
        {
            var csv = BuildCsv(session);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv",
                $"session-{sessionId:N}.csv");
        }

        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return File(Encoding.UTF8.GetBytes(json), "application/json",
            $"session-{sessionId:N}.json");
    }

    private static string BuildCsv(Core.Entities.WorkSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TicketKey,Title,IssueType,Priority,Status,BranchName,PRUrl,PRNumber,RetryCount,CreatedAt");
        foreach (var t in session.Tickets)
        {
            sb.AppendLine($"{t.JiraTicketKey},{Quote(t.JiraTicketTitle)},{t.JiraIssueType},{t.Priority}," +
                          $"{t.Status},{t.BranchName},{t.PullRequestUrl},{t.PullRequestNumber}," +
                          $"{t.RetryCount},{t.CreatedAt:O}");
        }
        return sb.ToString();
    }

    private static string Quote(string? s) => $"\"{(s ?? "").Replace("\"", "\"\"")}\"";
}
