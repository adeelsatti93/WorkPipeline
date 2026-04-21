using WorkPipeline.Core.DTOs;

namespace WorkPipeline.Core.Interfaces;

public interface IJiraService
{
    string BuildCredentials(string email, string apiToken);
    Task<List<JiraBoardDto>> GetBoardsAsync(string siteUrl, string base64Credentials);
    Task<List<JiraTicketDto>> GetTodoTicketsAsync(string siteUrl, string base64Credentials, string boardId, int maxResults = 100);
    Task<JiraTicketDto?> GetTicketAsync(string siteUrl, string base64Credentials, string key);
    Task TransitionTicketAsync(string siteUrl, string base64Credentials, string key, string targetStatusName);
}
