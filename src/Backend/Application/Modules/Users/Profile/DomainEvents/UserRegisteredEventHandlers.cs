using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Modules.Users.Profile.DomainEvents;

public sealed class UserRegisteredInTransactionEventHandler : INotificationHandler<UserRegisteredInTransactionEvent>
{
    private readonly ILogger<UserRegisteredInTransactionEventHandler> _logger;

    public UserRegisteredInTransactionEventHandler(ILogger<UserRegisteredInTransactionEventHandler> logger) =>
        _logger = logger;

    public Task Handle(UserRegisteredInTransactionEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Transactional domain event: user {UserId} registered ({Email})", notification.UserId, notification.Email);
        return Task.CompletedTask;
    }
}

public sealed class UserRegisteredAfterCommitEventHandler : INotificationHandler<UserRegisteredAfterCommitEvent>
{
    private readonly ILogger<UserRegisteredAfterCommitEventHandler> _logger;

    public UserRegisteredAfterCommitEventHandler(ILogger<UserRegisteredAfterCommitEventHandler> logger) =>
        _logger = logger;

    public Task Handle(UserRegisteredAfterCommitEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("After-commit domain event: user {UserId} registration committed", notification.UserId);
        return Task.CompletedTask;
    }
}
