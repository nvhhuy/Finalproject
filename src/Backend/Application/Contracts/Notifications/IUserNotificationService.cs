namespace Application.Contracts.Notifications;

public interface IUserNotificationService
{
    Task<Guid> CreateAndDispatchAsync(
        Guid userId,
        string title,
        string body,
        string? linkUrl = null,
        CancellationToken cancellationToken = default);
}