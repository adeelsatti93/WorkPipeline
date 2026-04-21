using WorkPipeline.Core.DTOs;

namespace WorkPipeline.Core.Interfaces;

public interface IGitHubService
{
    Task<List<GitHubRepoDto>> GetUserReposAsync(string token);
    Task<List<GitHubBranchDto>> GetBranchesAsync(string token, string owner, string repo);
    Task<bool> BranchExistsAsync(string token, string owner, string repo, string branchName);
    Task<PullRequestDto> CreatePullRequestAsync(string token, string owner, string repo, string head, string baseBranch, string title, string body);
    Task<PullRequestDto?> GetPullRequestAsync(string token, string owner, string repo, int prNumber);
    Task RegisterWebhookAsync(string token, string owner, string repo, string callbackUrl, string secret);
    bool VerifyWebhookSignature(string payload, string signature);
}
