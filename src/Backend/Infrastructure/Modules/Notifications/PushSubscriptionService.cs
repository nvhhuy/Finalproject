using Application.Contracts.Notifications;
using Application.Modules.Notifications;
using Domains.Notifications;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Modules.Notifications;

public sealed class PushSubscriptionService : IPushSubscriptionService
{
    private readonly ApplicationDbContext _db;
    private readonly WebPushOptions _options;

    public PushSubscriptionService(ApplicationDbContext db, IOptions<WebPushOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public string? GetVapidPublicKey()
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.VapidPublicKey))
            return null;
        return _options.VapidPublicKey.Trim();
    }

    public async Task RegisterAsync(Guid userId, string endpoint, string p256dh, string auth, string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint, cancellationToken);

        if (existing is not null)
            return;

        _db.PushSubscriptions.Add(new PushSubscription(userId, endpoint, p256dh, auth, userAgent));
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid userId, string endpoint, CancellationToken cancellationToken = default)
    {
        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint, cancellationToken);
        if (existing is null)
            return;

        _db.PushSubscriptions.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
