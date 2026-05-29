using Application.Contracts.Notifications;
using Application.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Application.Modules.Notifications;

public sealed record ProcessPendingNotificationDeliveriesCommand(int BatchSize = 200)
    : IRequest<Result<int, Error>>;

public sealed class ProcessPendingNotificationDeliveriesCommandHandler
    : IRequestHandler<ProcessPendingNotificationDeliveriesCommand, Result<int, Error>>
{
    private readonly INotificationDeliveryProcessor _processor;
    private readonly IApplicationReadDbContext _read;

    public ProcessPendingNotificationDeliveriesCommandHandler(
        IApplicationReadDbContext read,
        INotificationDeliveryProcessor processor)
    {
        _read = read;
        _processor = processor;
    }

    public async Task<Result<int, Error>> Handle(
        ProcessPendingNotificationDeliveriesCommand request,
        CancellationToken cancellationToken)
    {
        var batchSize = Math.Clamp(request.BatchSize, 1, 1_000);
        var cutoff = DateTime.UtcNow.AddDays(-7);

        var pendingIds = await _read.UserNotifications
            .AsNoTracking()
            .Where(n => n.LiveDeliveredAtUtc == null && n.CreatedAtUtc >= cutoff)
            .OrderBy(n => n.CreatedAtUtc)
            .Take(batchSize)
            .Select(n => n.Id)
            .ToListAsync(cancellationToken);

        var delivered = 0;
        foreach (var id in pendingIds)
            if (await _processor.TryDeliverAsync(id, cancellationToken))
                delivered++;

        return delivered;
    }
}