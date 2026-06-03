using Application.Contracts.Messaging;
using Application.Contracts.Notifications;
using Application.Modules.Notifications;
using Domains.Notifications.Events;
using Domains.UserAggregate;
using Infrastructure.Messaging;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Modules.Notifications;

public sealed class MemberNotificationBroadcastScheduler : IMemberNotificationBroadcaster
{
    private readonly ApplicationDbContext _db;
    private readonly IIntegrationEventOutboxWriter _outbox;
    private readonly IMemberNotificationBroadcastProcessor _processor;
    private readonly MemberNotificationBroadcastOptions _options;
    private readonly bool _messageBusEnabled;

    public MemberNotificationBroadcastScheduler(
        ApplicationDbContext db,
        IIntegrationEventOutboxWriter outbox,
        IMemberNotificationBroadcastProcessor processor,
        IOptions<MemberNotificationBroadcastOptions> options,
        IOptions<RabbitMqOptions> rabbitMq)
    {
        _db = db;
        _outbox = outbox;
        _processor = processor;
        _options = options.Value;
        _messageBusEnabled = rabbitMq.Value.Enabled;
    }

    public async Task<int> BroadcastToAllMembersAsync(
        string title,
        string body,
        string? linkUrl = null,
        CancellationToken cancellationToken = default)
    {
        var recipientCount = await CountActiveMembersAsync(cancellationToken);
        if (recipientCount == 0)
            return 0;

        var integrationEvent = new MemberNotificationBroadcastRequestedIntegrationEvent(title, body, linkUrl);
        await _outbox.EnqueueAsync(integrationEvent, cancellationToken);

        if (ShouldProcessInline())
        {
            await _processor.ProcessAsync(
                new MemberNotificationBroadcastPayload(
                    integrationEvent.EventId,
                    integrationEvent.Title,
                    integrationEvent.Body,
                    integrationEvent.LinkUrl),
                cancellationToken);
        }

        return recipientCount;
    }

    private bool ShouldProcessInline() =>
        _options.ProcessInlineInIntegrationTests || !_messageBusEnabled;

    internal static async Task<int> CountActiveMembersAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        return await db.Users
            .AsNoTracking()
            .Where(u =>
                u.Role == UserRoles.User
                && u.DeletedAtUtc == null
                && u.AccountClosedAtUtc == null
                && !u.IsLocked)
            .CountAsync(cancellationToken);
    }

    private Task<int> CountActiveMembersAsync(CancellationToken cancellationToken) =>
        CountActiveMembersAsync(_db, cancellationToken);
}
