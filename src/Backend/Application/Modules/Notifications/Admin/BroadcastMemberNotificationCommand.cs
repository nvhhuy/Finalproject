using Application.Contracts.Administration;
using Application.Contracts.Authorization;
using Application.Contracts.Notifications;
using Application.Modules.Users.Administration;

namespace Application.Modules.Notifications.Admin;

public sealed record BroadcastMemberNotificationCommand(
    Guid ActingAdminUserId,
    string Title,
    string Body,
    string? LinkUrl = null,
    string? ActorIpAddress = null)
    : IRequest<Result<BroadcastMemberNotificationResult, Error>>, IRequireAdmin;

public sealed record BroadcastMemberNotificationResult(int RecipientCount);

public sealed class BroadcastMemberNotificationCommandValidator
    : AbstractValidator<BroadcastMemberNotificationCommand>
{
    public BroadcastMemberNotificationCommandValidator()
    {
        RuleFor(x => x.ActingAdminUserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.LinkUrl)
            .MaximumLength(500)
            .Must(url => url is null || url.StartsWith('/'))
            .WithMessage("Link URL must be a relative path starting with '/'.")
            .When(x => !string.IsNullOrWhiteSpace(x.LinkUrl));
    }
}

public sealed class BroadcastMemberNotificationCommandHandler
    : IRequestHandler<BroadcastMemberNotificationCommand, Result<BroadcastMemberNotificationResult, Error>>
{
    private readonly IAdminAuditLogWriter _audit;
    private readonly IMemberNotificationBroadcaster _broadcaster;

    public BroadcastMemberNotificationCommandHandler(
        IMemberNotificationBroadcaster broadcaster,
        IAdminAuditLogWriter audit)
    {
        _broadcaster = broadcaster;
        _audit = audit;
    }

    public async Task<Result<BroadcastMemberNotificationResult, Error>> Handle(
        BroadcastMemberNotificationCommand request,
        CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();
        var body = request.Body.Trim();
        var linkUrl = string.IsNullOrWhiteSpace(request.LinkUrl) ? null : request.LinkUrl.Trim();

        var recipientCount = await _broadcaster.BroadcastToAllMembersAsync(
            title,
            body,
            linkUrl,
            cancellationToken);

        if (recipientCount == 0)
            return new ValidationError("recipients", "No active member accounts to notify.");

        _audit.Append(
            request.ActingAdminUserId,
            AdminAuditActions.MemberNotificationBroadcast,
            null,
            request.ActorIpAddress,
            $"recipients={recipientCount};title={title}");

        return new BroadcastMemberNotificationResult(recipientCount);
    }
}