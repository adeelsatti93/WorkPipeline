namespace WorkPipeline.Core.DTOs;

public record GitHubRepoDto(string FullName, string Name, string Owner, string DefaultBranch, bool Private);
public record GitHubBranchDto(string Name, bool IsDefault);
public record PullRequestDto(int Number, string Title, string Url, string State, bool Merged);
