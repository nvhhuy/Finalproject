using Api.Hubs;
using Application.Contracts.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace Api.Realtime;

public sealed class SignalRNotificationDispatcher : INotificationDispatcher
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotificationDispatcher(IHubContext<NotificationHub> hub) => _hub = hub;

    public Task DispatchAsync(Guid userId, LiveNotificationPayload payload, CancellationToken cancellationToken = default) =>
        _hub.Clients
            .Group(NotificationHub.UserGroup(userId))
            .SendAsync("notification", payload, cancellationToken);
}
