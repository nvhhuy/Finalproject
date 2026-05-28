using Application.Contracts.Notifications;

namespace Api.Realtime;

/// <summary>No-op when SignalR realtime is disabled.</summary>
public sealed class NullNotificationDispatcher : INotificationDispatcher
{
    public Task DispatchAsync(Guid userId, LiveNotificationPayload payload, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
