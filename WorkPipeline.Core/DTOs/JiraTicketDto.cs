namespace WorkPipeline.Core.DTOs;

public record JiraTicketDto(
    string Key,
    string Title,
    string? Description,
    string IssueType,
    string Priority,
    string? Assignee,
    string? AssigneeAvatarUrl,
    string Status,
    DateTime Created,
    string JiraUrl
);

public record JiraBoardDto(string Id, string Name, string? ProjectKey = null, string? BoardType = null);
public record JiraSiteDto(string Id, string Name, string Url);
