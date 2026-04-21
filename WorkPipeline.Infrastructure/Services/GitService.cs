using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;
using WorkPipeline.Core.Interfaces;

namespace WorkPipeline.Infrastructure.Services;

public class GitService(ILogger<GitService> logger) : IGitService
{
    public Task CloneOrFetchAsync(string repoUrl, string localPath, string accessToken)
    {
        var gitDir = Path.Combine(localPath, ".git");
        if (Directory.Exists(gitDir))
        {
            logger.LogInformation("Repo already exists at {Path} — fetching latest", localPath);
            using var repo = new Repository(localPath);
            var remote = repo.Network.Remotes["origin"];
            var fetchOptions = new FetchOptions { CredentialsProvider = Credentials(accessToken) };
            Commands.Fetch(repo, remote.Name, remote.FetchRefSpecs.Select(r => r.Specification), fetchOptions, null);
            logger.LogInformation("Fetch complete");
            return Task.CompletedTask;
        }

        logger.LogInformation("Cloning {Url} → {Path}", repoUrl, localPath);
        Directory.CreateDirectory(localPath);
        var cloneOptions = new CloneOptions();
        cloneOptions.FetchOptions.CredentialsProvider = Credentials(accessToken);
        Repository.Clone(repoUrl, localPath, cloneOptions);
        logger.LogInformation("Clone complete");
        return Task.CompletedTask;
    }

    public void ConfigureTokenAuth(string localPath, string repoUrl, string accessToken)
    {
        // Embed the token in the remote URL so `git push` and `gh` commands work
        // inside the Claude Code process without any separate credential store.
        var uri = new Uri(repoUrl);
        var authedUrl = $"{uri.Scheme}://x-access-token:{accessToken}@{uri.Host}{uri.PathAndQuery}";

        using var repo = new Repository(localPath);
        repo.Network.Remotes.Update("origin", r => r.Url = authedUrl);
        logger.LogInformation("Git remote auth configured for {Path}", localPath);
    }

    private static CredentialsHandler Credentials(string token) =>
        (_, _, _) => new UsernamePasswordCredentials { Username = "x-access-token", Password = token };
}
