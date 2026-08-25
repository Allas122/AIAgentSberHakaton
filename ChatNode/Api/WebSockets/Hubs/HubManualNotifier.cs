using ChatNode.Infrastructure.Manuals;
using Microsoft.AspNetCore.SignalR;

namespace ChatNode.Api.WebSockets.Hubs;

public class HubManualNotifier(IHubContext<ChatHub> chatHub) : IManualNotifier
{
    public Task ManualStatusAsync(Guid ownerId, ManualStatusUpdate update)
        => chatHub.Clients.Group($"user:{ownerId}").SendAsync("ManualStatus", new
        {
            ManualId = update.ManualId,
            Title = update.Title,
            Stage = update.Stage.ToString(),
            TotalChunks = update.TotalChunks,
            ProcessedChunks = update.ProcessedChunks,
            FailedChunks = update.FailedChunks,
            Detail = update.Detail,
            Gaps = update.Gaps
        });
}
