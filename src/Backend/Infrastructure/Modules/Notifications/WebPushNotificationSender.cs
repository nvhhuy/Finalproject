using System.Net;
using System.Text.Json;
using Application.Contracts.Notifications;
using Application.Modules.Notifications;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PushSubscriptionEntity = Domains.Notifications.PushSubscription;
using WebPush;

namespace Infrastructure.Modules.Notifications;

public sealed class WebPushNotificationSender : IWebPushNotificationSender
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<WebPushNotificationSender> _logger;
    private readonly WebPushOptions _options;
    private readonly WebPushClient _client = new();

    public WebPushNotificationSender(
        ApplicationDbContext db,
        IOptions<WebPushOptions> options,
        ILogger<WebPushNotificationSender> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.VapidPublicKey)
        && !string.IsNullOrWhiteSpace(_options.VapidPrivateKey);

    public async Task<bool> SendToUserAsync(Guid userId, LiveNotificationPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return false;

        var subs = await _db.PushSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

        if (subs.Count == 0)
            return false;

        var subject = string.IsNullOrWhiteSpace(_options.Subject)
            ? "mailto:support@localhost"
            : _options.Subject.Trim();
        var vapid = new VapidDetails(subject, _options.VapidPublicKey.Trim(), _options.VapidPrivateKey.Trim());

        var json = JsonSerializer.Serialize(new
        {
            id = payload.Id,
            title = payload.Title,
            body = payload.Body,
            linkUrl = payload.LinkUrl,
            createdAtUtc = payload.CreatedAtUtc
        });

        var anySent = false;
        var stale = new List<PushSubscriptionEntity>();

        foreach (var row in subs)
        {
            var subscription = new PushSubscription(row.Endpoint, row.P256dh, row.Auth);
            try
            {
                await _client.SendNotificationAsync(subscription, json, vapid, cancellationToken);
                anySent = true;
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                stale.Add(row);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Web push failed for user {UserId}", userId);
            }
        }

        if (stale.Count > 0)
        {
            _db.PushSubscriptions.RemoveRange(stale);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return anySent;
    }
}
