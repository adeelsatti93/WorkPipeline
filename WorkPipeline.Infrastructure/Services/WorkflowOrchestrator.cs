using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkPipeline.Core.Entities;
using WorkPipeline.Core.Enums;
using WorkPipeline.Core.Interfaces;
using WorkPipeline.Infrastructure.Data;
using WorkPipeline.Infrastructure.Hubs;

namespace WorkPipeline.Infrastructure.Services;

public class WorkflowOptions
{
    public int MaxRetryRounds { get; set; } = 2;
    public int TicketTimeoutMinutes { get; set; } = 30;
}

public class WorkflowOrchestrator(
    IServiceProvider serviceProvider,
    ILogger<WorkflowOrchestrator> logger,
    IOptions<WorkflowOptions> options)
{
    private readonly WorkflowOptions _opts = options.Value;

    // Repos are cloned into AppData so no user configuration is needed.
    private static string LocalRepoPath(string owner, string repo) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WorkPipeline", "repos", owner, repo);

    public async Task ProcessTicketAsync(Guid ticketWorkId, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(_opts.TicketTimeoutMinutes));

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jira = scope.ServiceProvider.GetRequiredService<IJiraService>();
        var github = scope.ServiceProvider.GetRequiredService<IGitHubService>();
        var git = scope.ServiceProvider.GetRequiredService<IGitService>();
        var claude = scope.ServiceProvider.GetRequiredService<IClaudeCodeService>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<WorkflowHub>>();

        var ticket = await db.TicketWorks
            .Include(t => t.Session).ThenInclude(s => s.Workspace)
            .FirstOrDefaultAsync(t => t.Id == ticketWorkId, cts.Token);

        if (ticket == null) return;

        var workspace = ticket.Session.Workspace;
        if (!workspace.IsFullyConfigured)
        {
            await FailTicketAsync(db, hub, ticket, "Workspace is not fully configured.", cts.Token);
            return;
        }

        var repoFullName = workspace.GitHubRepoFullName!;
        var parts = repoFullName.Split('/');
        var owner = parts[0];
        var repo = parts[1];
        var baseBranch = workspace.GitHubBranch ?? "main";
        var localPath = LocalRepoPath(owner, repo);
        var cloneUrl = $"https://github.com/{repoFullName}.git";
        var sessionGroup = $"session:{ticket.SessionId}";

        try
        {
            // ── Step 1: InProgress ────────────────────────────────────────────────
            await UpdateStatusAsync(db, hub, ticket, TicketWorkStatus.InProgress, sessionGroup, cts.Token);

            // ── Step 2: Clone / fetch repo ────────────────────────────────────────
            await SendStepAsync(hub, sessionGroup, ticketWorkId, "CloningRepo",
                $"Cloning / updating {repoFullName}…", cts.Token);

            await git.CloneOrFetchAsync(cloneUrl, localPath, workspace.GitHubToken!);
            git.ConfigureTokenAuth(localPath, cloneUrl, workspace.GitHubToken!);

            await SendStepAsync(hub, sessionGroup, ticketWorkId, "RepoReady",
                "Repository ready.", cts.Token);

            // ── Step 3: Branch name ───────────────────────────────────────────────
            var branchPrefix = ticket.JiraIssueType.Equals("Bug", StringComparison.OrdinalIgnoreCase) ? "bug" : "feature";
            var slug = Slugify($"{ticket.JiraTicketKey}-{ticket.JiraTicketTitle}");
            var branchName = $"{branchPrefix}/{slug}";

            if (await github.BranchExistsAsync(workspace.GitHubToken!, owner, repo, branchName))
                branchName = $"{branchName}-v{DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000}";

            ticket.BranchName = branchName;
            ticket.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cts.Token);

            // ── Step 4: Run Claude Code ───────────────────────────────────────────
            await SendStepAsync(hub, sessionGroup, ticketWorkId, "RunningClaude",
                $"Claude Code is implementing {ticket.JiraTicketKey}…", cts.Token);

            var prompt = BuildPrompt(ticket, branchName, baseBranch);
            await SaveTranscriptAsync(db, ticket.Id, "user", prompt, 100, 1, false, cts.Token);
            await hub.Clients.Group(sessionGroup).SendAsync("ClaudeStreamChunk",
                new { ticketWorkId, chunk = prompt, role = "user", roundNumber = 1 }, cts.Token);

            int seq = 101;
            await claude.ExecuteAsync(
                prompt,
                localPath,
                workspace.ClaudeApiKey!,
                workspace.GitHubToken!,
                async (role, chunk) =>
                {
                    await SaveTranscriptAsync(db, ticket.Id, role, chunk, seq++, 1, false, cts.Token);
                    await hub.Clients.Group(sessionGroup).SendAsync("ClaudeStreamChunk",
                        new { ticketWorkId, chunk, role, roundNumber = 1 }, cts.Token);
                },
                cts.Token);

            await hub.Clients.Group(sessionGroup).SendAsync("ClaudeStreamComplete",
                new { ticketWorkId, roundNumber = 1 }, cts.Token);

            // ── Step 5: Verify branch was pushed ─────────────────────────────────
            var branchPushed = await github.BranchExistsAsync(workspace.GitHubToken!, owner, repo, branchName);
            if (!branchPushed)
            {
                await FailTicketAsync(db, hub, ticket,
                    $"Branch '{branchName}' was not found on GitHub after Claude finished — push may have failed.",
                    cts.Token);
                return;
            }

            await hub.Clients.Group(sessionGroup).SendAsync("BranchCreated",
                new { ticketWorkId, branchName }, cts.Token);

            // ── Step 6: Create PR ─────────────────────────────────────────────────
            await UpdateStatusAsync(db, hub, ticket, TicketWorkStatus.CreatingPR, sessionGroup, cts.Token);
            await SendStepAsync(hub, sessionGroup, ticketWorkId, "CreatingPR",
                "Creating pull request on GitHub…", cts.Token);

            var pr = await github.CreatePullRequestAsync(
                workspace.GitHubToken!, owner, repo,
                branchName, baseBranch,
                $"[{ticket.JiraTicketKey}] {ticket.JiraTicketTitle}",
                BuildPrBody(ticket, workspace.JiraSiteUrl));

            ticket.PullRequestUrl = pr.Url;
            ticket.PullRequestNumber = pr.Number;
            ticket.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cts.Token);

            // ── Step 7: Transition Jira ───────────────────────────────────────────
            var inProgressStatus = workspace.JiraInProgressStatus ?? "In Progress";
            await jira.TransitionTicketAsync(workspace.JiraSiteUrl!, workspace.JiraCredentials!, ticket.JiraTicketKey, inProgressStatus);
            ticket.JiraTransitionedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cts.Token);

            // ── Step 8: Done ──────────────────────────────────────────────────────
            await UpdateStatusAsync(db, hub, ticket, TicketWorkStatus.AwaitingReview, sessionGroup, cts.Token);
            await hub.Clients.Group(sessionGroup).SendAsync("PrCreated",
                new { ticketWorkId, prUrl = pr.Url, prNumber = pr.Number }, cts.Token);

            ticket.Session.ProcessedTickets++;
            if (ticket.Session.ProcessedTickets >= ticket.Session.TotalTickets)
            {
                ticket.Session.CompletedAt = DateTime.UtcNow;
                var succeeded = await db.TicketWorks.CountAsync(
                    t => t.SessionId == ticket.SessionId && t.Status == TicketWorkStatus.AwaitingReview, cts.Token);
                var failed = await db.TicketWorks.CountAsync(
                    t => t.SessionId == ticket.SessionId && t.Status == TicketWorkStatus.Failed, cts.Token);
                await hub.Clients.Group(sessionGroup).SendAsync("SessionComplete",
                    new { sessionId = ticket.SessionId, total = ticket.Session.TotalTickets, succeeded, failed }, cts.Token);
            }
            await db.SaveChangesAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            await FailTicketAsync(db, hub, ticket, "Timed out after 30 minutes.", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process ticket {Key}", ticket.JiraTicketKey);
            await FailTicketAsync(db, hub, ticket, ex.Message, cancellationToken);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static async Task UpdateStatusAsync(AppDbContext db, IHubContext<WorkflowHub> hub,
        TicketWork ticket, TicketWorkStatus status, string group, CancellationToken ct)
    {
        ticket.Status = status;
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await hub.Clients.Group(group).SendAsync("TicketStatusChanged",
            new { ticketWorkId = ticket.Id, jiraKey = ticket.JiraTicketKey, status = status.ToString() }, ct);
    }

    private static async Task SendStepAsync(IHubContext<WorkflowHub> hub,
        string group, Guid ticketWorkId, string step, string message, CancellationToken ct)
    {
        await hub.Clients.Group(group).SendAsync("StepProgress",
            new { ticketWorkId, step, message, timestamp = DateTime.UtcNow }, ct);
        // Mirror into the stream panel so it's visible alongside Claude's output
        await hub.Clients.Group(group).SendAsync("ClaudeStreamChunk",
            new { ticketWorkId, chunk = message, role = "system", roundNumber = 0 }, ct);
    }

    private static async Task FailTicketAsync(AppDbContext db, IHubContext<WorkflowHub> hub,
        TicketWork ticket, string message, CancellationToken ct)
    {
        ticket.Status = TicketWorkStatus.Failed;
        ticket.ErrorMessage = message;
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.Session.ProcessedTickets++;
        await db.SaveChangesAsync(ct);
        await hub.Clients.Group($"session:{ticket.SessionId}").SendAsync("TicketFailed",
            new { ticketWorkId = ticket.Id, errorMessage = message, retryCount = ticket.RetryCount }, ct);
    }

    private static async Task SaveTranscriptAsync(AppDbContext db, Guid ticketWorkId,
        string role, string content, int seq, int round, bool isRetry, CancellationToken ct)
    {
        db.TranscriptEntries.Add(new ClaudeTranscriptEntry
        {
            TicketWorkId = ticketWorkId,
            Role = role,
            Content = content,
            Sequence = seq,
            RoundNumber = round,
            IsRetryRound = isRetry
        });
        await db.SaveChangesAsync(ct);
    }

    private static string BuildPrompt(TicketWork ticket, string branchName, string baseBranch) =>
        $"""
        You are an expert software engineer working in a git repository.
        The repository is already cloned locally — work directly in the current directory.

        ## Your task

        Implement the following Jira ticket, then push the branch so a PR can be created.

        Ticket:      {ticket.JiraTicketKey}
        Type:        {ticket.JiraIssueType}
        Priority:    {ticket.Priority}
        Title:       {ticket.JiraTicketTitle}

        Description:
        {ticket.JiraTicketDescription ?? "No description provided."}

        ## Steps to follow

        1. Create and switch to the feature branch:
               git checkout {baseBranch}
               git checkout -b {branchName}

        2. Explore the codebase, read relevant files, and implement ALL necessary changes.
           Follow the existing code conventions exactly.

        3. Stage and commit every change:
               git add -A
               git commit -m "[{ticket.JiraTicketKey}] {ticket.JiraTicketTitle}"

        4. Push the branch:
               git push origin {branchName}

        The git remote is already authenticated — run git commands directly without any credential prompts.
        Do NOT create a pull request; it will be created automatically after you push.
        """;

    private static string BuildPrBody(TicketWork ticket, string? siteUrl) =>
        $"""
        ## Jira Ticket
        [{ticket.JiraTicketKey}]({siteUrl}/browse/{ticket.JiraTicketKey}) — {ticket.JiraTicketTitle}

        ## Type
        {ticket.JiraIssueType} | Priority: {ticket.Priority}

        ## Description
        {ticket.JiraTicketDescription ?? "No description provided."}

        ---
        *Created automatically by [Work.Pipeline](https://github.com)*
        """;

    private static string Slugify(string input)
    {
        var slug = Regex.Replace(input.ToLower(), @"[^a-z0-9\-]", "-");
        slug = Regex.Replace(slug, @"-+", "-").Trim('-');
        return slug.Length > 60 ? slug[..60].TrimEnd('-') : slug;
    }
}
