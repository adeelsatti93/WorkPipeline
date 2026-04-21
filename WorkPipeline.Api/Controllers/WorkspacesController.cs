using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkPipeline.Core.DTOs;
using WorkPipeline.Core.Entities;
using WorkPipeline.Core.Interfaces;
using WorkPipeline.Infrastructure.Data;

namespace WorkPipeline.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/workspaces")]
public class WorkspacesController(
    AppDbContext db,
    IJiraService jira,
    IGitHubService github) : ControllerBase
{
    // ── CRUD ──────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetWorkspaces()
    {
        var workspaces = await db.Workspaces
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();
        return Ok(workspaces.Select(MapDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetWorkspace(Guid id)
    {
        var w = await db.Workspaces.FindAsync(id);
        return w == null ? NotFound() : Ok(MapDto(w));
    }

    [HttpPost]
    public async Task<IActionResult> CreateWorkspace([FromBody] CreateWorkspaceRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Name is required." });

        var workspace = new Workspace { Name = req.Name.Trim() };
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync();
        return Ok(MapDto(workspace));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteWorkspace(Guid id)
    {
        var workspace = await db.Workspaces.FindAsync(id);
        if (workspace == null) return NotFound();
        db.Workspaces.Remove(workspace);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── GitHub setup ──────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/github")]
    public async Task<IActionResult> SaveGitHub(Guid id, [FromBody] SaveWorkspaceGitHubRequest req)
    {
        var workspace = await db.Workspaces.FindAsync(id);
        if (workspace == null) return NotFound();

        // Validate token by fetching repos
        try { await github.GetUserReposAsync(req.PersonalAccessToken); }
        catch { return BadRequest(new { message = "Invalid GitHub token or network error." }); }

        workspace.GitHubToken = req.PersonalAccessToken;
        workspace.GitHubRepoFullName = req.RepoFullName;
        workspace.GitHubBranch = req.Branch;
        workspace.RepoLocalPath = req.LocalPath;
        workspace.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Register webhook (best-effort)
        try
        {
            var parts = req.RepoFullName.Split('/');
            var webhookUrl = $"{Request.Scheme}://{Request.Host}/api/webhooks/github";
            await github.RegisterWebhookAsync(req.PersonalAccessToken, parts[0], parts[1], webhookUrl, "webhook-secret");
        }
        catch { /* webhook is optional */ }

        return Ok(MapDto(workspace));
    }

    [HttpGet("{id:guid}/github/repos")]
    public async Task<IActionResult> GetGitHubRepos(Guid id, [FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { message = "Token is required." });
        var repos = await github.GetUserReposAsync(token);
        return Ok(repos);
    }

    [HttpGet("{id:guid}/github/repos/{owner}/{repo}/branches")]
    public async Task<IActionResult> GetGitHubBranches(Guid id, string owner, string repo, [FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { message = "Token is required." });
        var branches = await github.GetBranchesAsync(token, owner, repo);
        return Ok(branches);
    }

    // ── Jira setup ────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/jira")]
    public async Task<IActionResult> SaveJira(Guid id, [FromBody] SaveWorkspaceJiraRequest req)
    {
        var workspace = await db.Workspaces.FindAsync(id);
        if (workspace == null) return NotFound();

        var creds = jira.BuildCredentials(req.Email, req.ApiToken);

        // Validate by fetching boards
        try { await jira.GetBoardsAsync(req.SiteUrl, creds); }
        catch { return BadRequest(new { message = "Could not connect to Jira. Check your site URL, email, and token." }); }

        workspace.JiraEmail = req.Email;
        workspace.JiraCredentials = creds;
        workspace.JiraSiteUrl = req.SiteUrl.TrimEnd('/');
        workspace.JiraBoardId = req.BoardId;
        workspace.JiraBoardName = req.BoardName;
        workspace.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(MapDto(workspace));
    }

    [HttpGet("{id:guid}/jira/boards")]
    public async Task<IActionResult> GetJiraBoards(Guid id,
        [FromQuery] string email, [FromQuery] string apiToken, [FromQuery] string siteUrl)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(apiToken) || string.IsNullOrWhiteSpace(siteUrl))
            return BadRequest(new { message = "email, apiToken and siteUrl are required." });

        var creds = jira.BuildCredentials(email, apiToken);
        var boards = await jira.GetBoardsAsync(siteUrl.TrimEnd('/'), creds);
        return Ok(boards);
    }

    // ── Claude setup ──────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/claude")]
    public async Task<IActionResult> SaveClaude(Guid id, [FromBody] SaveWorkspaceClaudeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ApiKey) || !req.ApiKey.StartsWith("sk-ant-"))
            return BadRequest(new { message = "Invalid API key format. Must start with sk-ant-" });

        var workspace = await db.Workspaces.FindAsync(id);
        if (workspace == null) return NotFound();

        workspace.ClaudeApiKey = req.ApiKey;
        workspace.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(MapDto(workspace));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WorkspaceDto MapDto(Workspace w) => new(
        w.Id,
        w.Name,
        w.IsFullyConfigured,
        w.GitHubToken != null && w.GitHubRepoFullName != null,
        w.JiraCredentials != null && w.JiraSiteUrl != null && w.JiraBoardId != null,
        w.ClaudeApiKey != null,
        w.GitHubRepoFullName,
        w.GitHubBranch,
        w.JiraSiteUrl,
        w.JiraBoardName,
        w.CreatedAt
    );
}
