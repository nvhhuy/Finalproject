using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>Placeholder hub for future notifications or chat; clients pass JWT as <c>?access_token=</c> on WebSocket URL.</summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    public Task<string> PingAsync() => Task.FromResult("pong");
}
