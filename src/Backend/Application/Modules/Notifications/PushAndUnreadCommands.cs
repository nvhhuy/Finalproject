using Application.Contracts.Notifications;
using Application.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Application.Modules.Notifications;

public sealed record GetWebPushVapidPublicKeyQuery : IRequest<Result<string?, Error>>;

public sealed class GetWebPushVapidPublicKeyQueryHandler
    : IRequestHandler<GetWebPushVapidPublicKeyQuery, Result<string?, Error>>
{
    private readonly IPushSubscriptionService _push;

    public GetWebPushVapidPublicKeyQueryHandler(IPushSubscriptionService push) => _push = push;

    public Task<Result<string?, Error>> Handle(GetWebPushVapidPublicKeyQuery request,
        CancellationToken cancellationToken) =>
        Task.FromResult<Result<string?, Error>>(_push.GetVapidPublicKey());
}

public sealed record RegisterPushSubscriptionCommand(
    Guid UserId,
    string Endpoint,
    string P256dh,
    string Auth,
    string? UserAgent) : IRequest<Result<bool, Error>>;

public sealed class RegisterPushSubscriptionCommandHandler
    : IRequestHandler<RegisterPushSubscriptionCommand, Result<bool, Error>>
{
    private readonly IPushSubscriptionService _push;

    public RegisterPushSubscriptionCommandHandler(IPushSubscriptionService push) => _push = push;

    public async Task<Result<bool, Error>> Handle(RegisterPushSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint)
            || string.IsNullOrWhiteSpace(request.P256dh)
            || string.IsNullOrWhiteSpace(request.Auth))
            return new Error("BadRequest", 400, "Push subscription keys are required.");

        await _push.RegisterAsync(
            request.UserId,
            request.Endpoint.Trim(),
            request.P256dh.Trim(),
            request.Auth.Trim(),
            request.UserAgent,
            cancellationToken);
        return true;
    }
}

public sealed record RemovePushSubscriptionCommand(Guid UserId, string Endpoint) : IRequest<Result<bool, Error>>;

public sealed class RemovePushSubscriptionCommandHandler
    : IRequestHandler<RemovePushSubscriptionCommand, Result<bool, Error>>
{
    private readonly IPushSubscriptionService _push;

    public RemovePushSubscriptionCommandHandler(IPushSubscriptionService push) => _push = push;

    public async Task<Result<bool, Error>> Handle(RemovePushSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint))
            return new Error("BadRequest", 400, "Endpoint is required.");

        await _push.RemoveAsync(request.UserId, request.Endpoint.Trim(), cancellationToken);
        return true;
    }
}

public sealed record GetNotificationUnreadCountQuery(Guid UserId) : IRequest<Result<int, Error>>;

public sealed class GetNotificationUnreadCountQueryHandler
    : IRequestHandler<GetNotificationUnreadCountQuery, Result<int, Error>>
{
    private readonly IApplicationReadDbContext _read;

    public GetNotificationUnreadCountQueryHandler(IApplicationReadDbContext read) => _read = read;

    public async Task<Result<int, Error>> Handle(GetNotificationUnreadCountQuery request,
        CancellationToken cancellationToken)
    {
        var count = await _read.UserNotifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == request.UserId && n.ReadAtUtc == null, cancellationToken);
        return count;
    }
}
