namespace WorkPipeline.Core.DTOs;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Username);

// ── Workspace DTOs ────────────────────────────────────────────────────────────

public record CreateWorkspaceRequest(string Name);

public record WorkspaceDto(
    Guid Id,
    string Name,
    bool IsFullyConfigured,
    bool HasGitHub,
    bool HasJira,
    bool HasClaude,
    string? GitHubRepoFullName,
    string? GitHubBranch,
    string? JiraSiteUrl,
    string? JiraBoardName,
    DateTime CreatedAt
);

public record SaveWorkspaceGitHubRequest(
    string PersonalAccessToken,
    string RepoFullName,
    string Branch,
    string LocalPath
);

public record SaveWorkspaceJiraRequest(
    string Email,
    string ApiToken,
    string SiteUrl,
    string BoardId,
    string BoardName
);

public record SaveWorkspaceClaudeRequest(string ApiKey);

public record ValidateJiraRequest(string Email, string ApiToken, string SiteUrl);
public record ValidateGitHubRequest(string PersonalAccessToken);
