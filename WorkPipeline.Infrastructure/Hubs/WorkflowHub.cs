using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WorkPipeline.Infrastructure.Hubs;

[Authorize]
public class WorkflowHub : Hub
{
    public async Task JoinSession(string sessionId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{sessionId}");

    public async Task LeaveSession(string sessionId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session:{sessionId}");
}
