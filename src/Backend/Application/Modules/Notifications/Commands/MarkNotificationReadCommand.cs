using Application.Contracts.Notifications;

namespace Application.Modules.Notifications.Commands;

public sealed record MarkNotificationReadCommand(Guid UserId, Guid NotificationId) : IRequest<Result<bool, Error>>;

public sealed class MarkNotificationReadCommandHandler
    : IRequestHandler<MarkNotificationReadCommand, Result<bool, Error>>
{
    private readonly IUserNotificationWriter _writer;

    public MarkNotificationReadCommandHandler(IUserNotificationWriter writer)
    {
        _writer = writer;
    }

    public async Task<Result<bool, Error>> Handle(
        MarkNotificationReadCommand request,
        CancellationToken cancellationToken)
    {
        var ok = await _writer.MarkReadAsync(request.UserId, request.NotificationId, cancellationToken);
        if (!ok)
            return new NotFoundError("Notification was not found.");

        return true;
    }
}