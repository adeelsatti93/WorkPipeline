using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using WorkPipeline.Core.DTOs;
using WorkPipeline.Core.Interfaces;

namespace WorkPipeline.Infrastructure.Services;

public class JiraService(IHttpClientFactory httpClientFactory) : IJiraService
{
    public string BuildCredentials(string email, string apiToken) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));

    public async Task<List<JiraBoardDto>> GetBoardsAsync(string siteUrl, string base64Credentials)
    {
        var client = GetClient(base64Credentials);
        var base_ = siteUrl.TrimEnd('/');
        var boards = new List<JiraBoardDto>();
        var startAt = 0;
        const int pageSize = 50;

        while (true)
        {
            var resp = await client.GetFromJsonAsync<JsonElement>(
                $"{base_}/rest/agile/1.0/board?maxResults={pageSize}&startAt={startAt}");

            foreach (var b in resp.GetProperty("values").EnumerateArray())
            {
                var location = b.TryGetProperty("location", out var loc) ? loc : default;
                var projectKey = location.ValueKind == JsonValueKind.Object &&
                    location.TryGetProperty("projectKey", out var pk) ? pk.GetString() : null;
                var boardType = b.TryGetProperty("type", out var bt) ? bt.GetString() : null;

                boards.Add(new JiraBoardDto(
                    b.GetProperty("id").GetInt32().ToString(),
                    b.GetProperty("name").GetString()!,
                    projectKey,
                    boardType));
            }

            var isLast = resp.TryGetProperty("isLast", out var last) && last.GetBoolean();
            var total = resp.TryGetProperty("total", out var t) ? t.GetInt32() : (int?)null;

            if (isLast || (total.HasValue && boards.Count >= total.Value)) break;
            startAt += pageSize;
        }

        return boards;
    }

    public async Task<List<JiraTicketDto>> GetTodoTicketsAsync(string siteUrl, string base64Credentials, string boardId, int maxResults = 100)
    {
        var client = GetClient(base64Credentials);
        var base_ = siteUrl.TrimEnd('/');

        // Use the Agile board issues endpoint — works for all board types (scrum, kanban, simple/next-gen)
        // JQL boardProjects() only works for classic boards and fails silently for next-gen
        var jql = Uri.EscapeDataString("status = \"To Do\" ORDER BY priority DESC");
        var url = $"{base_}/rest/agile/1.0/board/{boardId}/issue?jql={jql}&maxResults={maxResults}" +
                  "&fields=summary,description,issuetype,priority,assignee,status,created";

        var resp = await client.GetFromJsonAsync<JsonElement>(url);
        var tickets = new List<JiraTicketDto>();

        foreach (var issue in resp.GetProperty("issues").EnumerateArray())
        {
            var fields = issue.GetProperty("fields");
            var key = issue.GetProperty("key").GetString()!;
            tickets.Add(new JiraTicketDto(
                key,
                fields.GetProperty("summary").GetString()!,
                ExtractDescription(fields),
                fields.TryGetProperty("issuetype", out var it) ? it.GetProperty("name").GetString()! : "Task",
                fields.TryGetProperty("priority", out var p) ? p.GetProperty("name").GetString()! : "Medium",
                fields.TryGetProperty("assignee", out var a) && a.ValueKind != JsonValueKind.Null
                    ? a.GetProperty("displayName").GetString() : null,
                fields.TryGetProperty("assignee", out var av) && av.ValueKind != JsonValueKind.Null &&
                    av.TryGetProperty("avatarUrls", out var urls) &&
                    urls.TryGetProperty("48x48", out var url48)
                    ? url48.GetString() : null,
                fields.GetProperty("status").GetProperty("name").GetString()!,
                fields.TryGetProperty("created", out var created)
                    ? DateTime.Parse(created.GetString()!) : DateTime.UtcNow,
                $"{base_}/browse/{key}"
            ));
        }
        return tickets;
    }

    public async Task<JiraTicketDto?> GetTicketAsync(string siteUrl, string base64Credentials, string key)
    {
        var client = GetClient(base64Credentials);
        var base_ = siteUrl.TrimEnd('/');
        var resp = await client.GetFromJsonAsync<JsonElement>($"{base_}/rest/api/3/issue/{key}");

        var fields = resp.GetProperty("fields");
        return new JiraTicketDto(
            key,
            fields.GetProperty("summary").GetString()!,
            ExtractDescription(fields),
            fields.TryGetProperty("issuetype", out var it) ? it.GetProperty("name").GetString()! : "Task",
            fields.TryGetProperty("priority", out var p) ? p.GetProperty("name").GetString()! : "Medium",
            null, null,
            fields.GetProperty("status").GetProperty("name").GetString()!,
            DateTime.UtcNow,
            $"{base_}/browse/{key}"
        );
    }

    public async Task<List<string>> GetBoardStatusesAsync(string siteUrl, string base64Credentials, string boardId)
    {
        var client = GetClient(base64Credentials);
        var base_ = siteUrl.TrimEnd('/');

        // Fetch the board's sprint/column configuration to get real status names
        var resp = await client.GetFromJsonAsync<JsonElement>(
            $"{base_}/rest/agile/1.0/board/{boardId}/configuration");

        var statuses = new List<string>();
        if (resp.TryGetProperty("columnConfig", out var colConfig) &&
            colConfig.TryGetProperty("columns", out var columns))
        {
            foreach (var col in columns.EnumerateArray())
            {
                if (col.TryGetProperty("statuses", out var colStatuses))
                {
                    foreach (var s in colStatuses.EnumerateArray())
                    {
                        if (s.TryGetProperty("id", out var sid))
                        {
                            // Resolve status name from id
                            var statusId = sid.GetString();
                            if (!string.IsNullOrEmpty(statusId))
                            {
                                try
                                {
                                    var statusResp = await client.GetFromJsonAsync<JsonElement>(
                                        $"{base_}/rest/api/3/status/{statusId}");
                                    if (statusResp.TryGetProperty("name", out var sname))
                                        statuses.Add(sname.GetString()!);
                                }
                                catch { /* skip unresolvable statuses */ }
                            }
                        }
                    }
                }
            }
        }

        // Fallback: fetch all instance statuses if board config returned nothing
        if (statuses.Count == 0)
        {
            var allStatuses = await client.GetFromJsonAsync<JsonElement[]>(
                $"{base_}/rest/api/3/status");
            if (allStatuses != null)
                statuses.AddRange(allStatuses
                    .Where(s => s.TryGetProperty("name", out _))
                    .Select(s => s.GetProperty("name").GetString()!)
                    .Where(n => !string.IsNullOrEmpty(n)));
        }

        return statuses.Distinct().ToList();
    }

    public async Task TransitionTicketAsync(string siteUrl, string base64Credentials, string key, string targetStatusName)
    {
        var client = GetClient(base64Credentials);
        var base_ = $"{siteUrl.TrimEnd('/')}/rest/api/3";

        var transitions = await client.GetFromJsonAsync<JsonElement>($"{base_}/issue/{key}/transitions");
        var transition = transitions.GetProperty("transitions").EnumerateArray()
            .FirstOrDefault(t => string.Equals(
                t.GetProperty("name").GetString(), targetStatusName,
                StringComparison.OrdinalIgnoreCase));

        if (transition.ValueKind == JsonValueKind.Undefined) return;

        var body = new { transition = new { id = transition.GetProperty("id").GetString() } };
        await client.PostAsync($"{base_}/issue/{key}/transitions",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
    }

    private HttpClient GetClient(string base64Credentials)
    {
        var client = httpClientFactory.CreateClient("jira");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", base64Credentials);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        return client;
    }

    private static string? ExtractDescription(JsonElement fields)
    {
        if (!fields.TryGetProperty("description", out var desc) || desc.ValueKind == JsonValueKind.Null)
            return null;
        // ADF (Atlassian Document Format) object — classic projects
        if (desc.ValueKind == JsonValueKind.Object && desc.TryGetProperty("content", out var content))
            return ExtractAdfText(content);
        // Plain string — next-gen / simple projects or already serialised text
        if (desc.ValueKind == JsonValueKind.String)
            return desc.GetString();
        return null;
    }

    private static string ExtractAdfText(JsonElement content)
    {
        var sb = new StringBuilder();
        foreach (var node in content.EnumerateArray())
        {
            if (node.TryGetProperty("content", out var inner))
                sb.Append(ExtractAdfText(inner));
            if (node.TryGetProperty("text", out var text))
                sb.AppendLine(text.GetString());
        }
        return sb.ToString().Trim();
    }
}
