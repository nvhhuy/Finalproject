using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>User-scoped realtime notifications. JWT via <c>?access_token=</c> on WebSocket URL.</summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    public const string ReceiveMethod = "notification";

    public static string UserGroup(Guid userId)
    {
        return $"user:{userId:D}";
    }

    public override async Task OnConnectedAsync()
    {
        if (TryGetUserId(out var userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetUserId(out var userId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId));

        await base.OnDisconnectedAsync(exception);
    }

    public Task<string> PingAsync()
    {
        return Task.FromResult("pong");
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out userId);
    }
}