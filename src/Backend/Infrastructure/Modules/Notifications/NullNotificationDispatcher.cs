using Application.Contracts.Notifications;

namespace Infrastructure.Modules.Notifications;

/// <summary>No-op when SignalR realtime is disabled or the host does not run a notification hub.</summary>
public sealed class NullNotificationDispatcher : INotificationDispatcher
{
    public Task DispatchAsync(Guid userId, LiveNotificationPayload payload, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
