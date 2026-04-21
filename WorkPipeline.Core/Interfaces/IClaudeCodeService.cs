namespace WorkPipeline.Core.Interfaces;

public interface IClaudeCodeService
{
    /// <summary>
    /// Spawns the Claude Code CLI in <paramref name="workingDirectory"/>.
    /// Claude reads/writes files and runs git commands directly in that directory.
    /// Output chunks are streamed live via <paramref name="onChunk"/>.
    /// </summary>
    Task ExecuteAsync(
        string prompt,
        string workingDirectory,
        string apiKey,
        string githubToken,
        Func<string, string, Task> onChunk,
        CancellationToken cancellationToken);
}
