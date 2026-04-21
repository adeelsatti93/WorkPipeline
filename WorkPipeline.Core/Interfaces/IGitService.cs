namespace WorkPipeline.Core.Interfaces;

public interface IGitService
{
    /// <summary>Clones the repo if it doesn't exist locally, otherwise fetches latest.</summary>
    Task CloneOrFetchAsync(string repoUrl, string localPath, string accessToken);

    /// <summary>
    /// Rewrites the origin remote URL to embed the access token so Claude Code's
    /// git push / gh commands work without separate credential setup.
    /// </summary>
    void ConfigureTokenAuth(string localPath, string repoUrl, string accessToken);
}
