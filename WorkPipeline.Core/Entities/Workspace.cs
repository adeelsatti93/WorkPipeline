namespace WorkPipeline.Core.Entities;

public class Workspace
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    // GitHub
    public string? GitHubToken { get; set; }
    public string? GitHubRepoFullName { get; set; }
    public string? GitHubBranch { get; set; }
    public string? RepoLocalPath { get; set; }

    // Jira — Basic Auth: base64(email:apiToken)
    public string? JiraCredentials { get; set; }
    public string? JiraEmail { get; set; }
    public string? JiraSiteUrl { get; set; }
    public string? JiraBoardId { get; set; }
    public string? JiraBoardName { get; set; }

    // Claude
    public string? ClaudeApiKey { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorkSession> Sessions { get; set; } = new List<WorkSession>();

    public bool IsFullyConfigured =>
        GitHubToken != null && GitHubRepoFullName != null && GitHubBranch != null &&
        JiraCredentials != null && JiraSiteUrl != null && JiraBoardId != null &&
        ClaudeApiKey != null;
}
