using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using WorkPipeline.Core.Interfaces;

namespace WorkPipeline.Infrastructure.Services;

public class ClaudeCodeService : IClaudeCodeService
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    // Resolved once at startup so spawned processes don't rely on inherited PATH.
    private static readonly string ClaudePath = ResolveClaudePath();

    public async Task ExecuteAsync(
        string prompt,
        string workingDirectory,
        string apiKey,
        string githubToken,
        Func<string, string, Task> onChunk,
        CancellationToken cancellationToken)
    {
        if (ClaudePath == "claude" && IsWindows)
            await onChunk("system", "[Warning] claude.exe path could not be resolved — attempting anyway.");
        else
            await onChunk("system", $"[Info] Using Claude at: {ClaudePath}");

        var psi = BuildPsi(workingDirectory);
        psi.Environment["ANTHROPIC_API_KEY"] = apiKey;
        psi.Environment["GITHUB_TOKEN"] = githubToken;
        psi.Environment["GH_TOKEN"] = githubToken;

        using var process = new Process { StartInfo = psi };
        process.Start();

        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        while (!process.StandardOutput.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;

                switch (type)
                {
                    case "assistant":
                        if (root.TryGetProperty("message", out var msg) &&
                            msg.TryGetProperty("content", out var content) &&
                            content.GetArrayLength() > 0)
                        {
                            var text = content[0].TryGetProperty("text", out var tx) ? tx.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(text))
                                await onChunk("assistant", text);
                        }
                        break;

                    case "text":
                        var txt = root.TryGetProperty("text", out var tp) ? tp.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(txt))
                            await onChunk("assistant", txt);
                        break;

                    case "tool_use":
                        var toolName = root.TryGetProperty("tool_name", out var tn) ? tn.GetString() : null;
                        if (!string.IsNullOrEmpty(toolName))
                            await onChunk("system", $"🔧 {toolName}");
                        break;

                    case "result":
                        if (root.TryGetProperty("subtype", out var sub) && sub.GetString() == "error_during_execution")
                        {
                            var errMsg = root.TryGetProperty("error", out var err) ? err.GetString() : "Claude CLI error";
                            await onChunk("system", $"[Error] {errMsg}");
                        }
                        else if (root.TryGetProperty("result", out var result))
                        {
                            var resultText = result.GetString() ?? "";
                            if (!string.IsNullOrEmpty(resultText))
                                await onChunk("assistant", resultText);
                        }
                        break;
                        // system/init events — skip silently
                }
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(line))
                    await onChunk("system", line);
            }
        }

        await process.WaitForExitAsync(cancellationToken);

        var stderr = await stderrTask;
        if (!string.IsNullOrWhiteSpace(stderr))
            await onChunk("system", $"[stderr] {stderr}");

        if (process.ExitCode != 0)
            await onChunk("system", $"[Claude CLI exited with code {process.ExitCode}]");
    }

    private static ProcessStartInfo BuildPsi(string workingDir)
    {
        const string claudeArgs = "--print --verbose --output-format stream-json --dangerously-skip-permissions";

        // If we found claude.exe, invoke it directly so stdin pipes reliably.
        // If we only found a .cmd shim, wrap with cmd.exe as a fallback.
        string fileName, arguments;
        if (IsWindows && ClaudePath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "cmd.exe";
            arguments = $"/c \"{ClaudePath}\" {claudeArgs}";
        }
        else
        {
            fileName = ClaudePath;
            arguments = claudeArgs;
        }

        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static string ResolveClaudePath()
    {
        if (!IsWindows) return "claude";

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var username = Environment.UserName;

        // .cmd shim locations — we'll derive claude.exe from each
        var cmdShimPaths = new[]
        {
            @"C:\nvm4w\nodejs\claude.cmd",
            Path.Combine(localAppData, "nvm", "nodejs", "claude.cmd"),
            Path.Combine(appData, "npm", "claude.cmd"),
            Path.Combine(@"C:\Users", username, @"AppData\Roaming\npm\claude.cmd"),
            @"C:\Program Files\nodejs\claude.cmd",
        };

        foreach (var cmdPath in cmdShimPaths)
        {
            if (!File.Exists(cmdPath)) continue;

            // claude.cmd lives next to node_modules; claude.exe is inside it
            var nodeDir = Path.GetDirectoryName(cmdPath)!;
            var exePath = Path.Combine(nodeDir, "node_modules", "@anthropic-ai", "claude-code", "bin", "claude.exe");
            if (File.Exists(exePath)) return exePath;

            // Fallback: return the .cmd path itself (will use cmd.exe wrapper below)
            return cmdPath;
        }

        // Ask npm where its global bin is
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c npm prefix -g",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            var prefix = p?.StandardOutput.ReadToEnd().Trim();
            p?.WaitForExit(5000);
            if (!string.IsNullOrEmpty(prefix))
            {
                var exePath = Path.Combine(prefix, "node_modules", "@anthropic-ai", "claude-code", "bin", "claude.exe");
                if (File.Exists(exePath)) return exePath;
                var cmdPath = Path.Combine(prefix, "claude.cmd");
                if (File.Exists(cmdPath)) return cmdPath;
            }
        }
        catch { }

        // Scan PATH
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var nodeDir = dir.Trim();
                var exePath = Path.Combine(nodeDir, "node_modules", "@anthropic-ai", "claude-code", "bin", "claude.exe");
                if (File.Exists(exePath)) return exePath;
                var cmdPath = Path.Combine(nodeDir, "claude.cmd");
                if (File.Exists(cmdPath)) return cmdPath;
            }
            catch { }
        }

        return "claude";
    }
}
