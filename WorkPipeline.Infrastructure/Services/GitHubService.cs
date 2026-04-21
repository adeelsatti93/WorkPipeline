using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WorkPipeline.Core.DTOs;
using WorkPipeline.Core.Interfaces;

namespace WorkPipeline.Infrastructure.Services;

public class GitHubService(IHttpClientFactory httpClientFactory, IConfiguration config) : IGitHubService
{
    private const string GitHubApi = "https://api.github.com";

    public async Task<List<GitHubRepoDto>> GetUserReposAsync(string token)
    {
        var client = GetClient(token);
        var repos = new List<GitHubRepoDto>();
        var page = 1;

        while (true)
        {
            var resp = await client.GetFromJsonAsync<JsonElement[]>(
                $"{GitHubApi}/user/repos?per_page=100&page={page}&sort=updated&affiliation=owner,collaborator,organization_member");
            if (resp == null || resp.Length == 0) break;

            repos.AddRange(resp.Select(r => new GitHubRepoDto(
                r.GetProperty("full_name").GetString()!,
                r.GetProperty("name").GetString()!,
                r.GetProperty("owner").GetProperty("login").GetString()!,
                r.GetProperty("default_branch").GetString()!,
                r.GetProperty("private").GetBoolean()
            )));
            if (resp.Length < 100) break;
            page++;
        }
        return repos;
    }

    public async Task<List<GitHubBranchDto>> GetBranchesAsync(string token, string owner, string repo)
    {
        var client = GetClient(token);
        var repoInfo = await client.GetFromJsonAsync<JsonElement>($"{GitHubApi}/repos/{owner}/{repo}");
        var defaultBranch = repoInfo.GetProperty("default_branch").GetString()!;

        var branches = await client.GetFromJsonAsync<JsonElement[]>(
            $"{GitHubApi}/repos/{owner}/{repo}/branches?per_page=100");

        return branches?.Select(b =>
        {
            var name = b.GetProperty("name").GetString()!;
            return new GitHubBranchDto(name, name == defaultBranch);
        }).OrderByDescending(b => b.IsDefault).ToList() ?? [];
    }

    public async Task<bool> BranchExistsAsync(string token, string owner, string repo, string branchName)
    {
        var client = GetClient(token);
        var resp = await client.GetAsync(
            $"{GitHubApi}/repos/{owner}/{repo}/branches/{Uri.EscapeDataString(branchName)}");
        return resp.IsSuccessStatusCode;
    }

    public async Task<PullRequestDto> CreatePullRequestAsync(string token, string owner, string repo,
        string head, string baseBranch, string title, string body)
    {
        var client = GetClient(token);
        var payload = new { title, body, head, @base = baseBranch, draft = false };

        var resp = await client.PostAsync(
            $"{GitHubApi}/repos/{owner}/{repo}/pulls",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return new PullRequestDto(
            json.GetProperty("number").GetInt32(),
            json.GetProperty("title").GetString()!,
            json.GetProperty("html_url").GetString()!,
            json.GetProperty("state").GetString()!,
            false
        );
    }

    public async Task<PullRequestDto?> GetPullRequestAsync(string token, string owner, string repo, int prNumber)
    {
        var client = GetClient(token);
        var resp = await client.GetAsync($"{GitHubApi}/repos/{owner}/{repo}/pulls/{prNumber}");
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return new PullRequestDto(
            json.GetProperty("number").GetInt32(),
            json.GetProperty("title").GetString()!,
            json.GetProperty("html_url").GetString()!,
            json.GetProperty("state").GetString()!,
            json.TryGetProperty("merged", out var m) && m.GetBoolean()
        );
    }

    public async Task RegisterWebhookAsync(string token, string owner, string repo, string callbackUrl, string secret)
    {
        var client = GetClient(token);
        var payload = new
        {
            name = "web",
            active = true,
            events = new[] { "pull_request" },
            config = new { url = callbackUrl, content_type = "json", secret, insecure_ssl = "0" }
        };

        await client.PostAsync(
            $"{GitHubApi}/repos/{owner}/{repo}/hooks",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
    }

    public bool VerifyWebhookSignature(string payload, string signature)
    {
        var secret = config["GitHub:WebhookSecret"];
        if (string.IsNullOrEmpty(secret)) return true;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expected = "sha256=" + Convert.ToHexString(hash).ToLower();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    private HttpClient GetClient(string token)
    {
        var client = httpClientFactory.CreateClient("github");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WorkPipeline/1.0");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }
}
