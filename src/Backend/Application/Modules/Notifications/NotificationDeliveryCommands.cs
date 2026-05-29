using Application.Contracts.Notifications;
using Application.Contracts.Persistence;
using Domains.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Common;
using Shared.Errors;

namespace Application.Modules.Notifications;

public sealed record ProcessPendingNotificationDeliveriesCommand(int BatchSize = 200)
    : IRequest<Result<int, Error>>;

public sealed class ProcessPendingNotificationDeliveriesCommandHandler
    : IRequestHandler<ProcessPendingNotificationDeliveriesCommand, Result<int, Error>>
{
    private readonly IApplicationReadDbContext _read;
    private readonly INotificationDeliveryProcessor _processor;

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
        int batchSize = Math.Clamp(request.BatchSize, 1, 1_000);
        DateTime cutoff = DateTime.UtcNow.AddDays(-7);

        List<Guid> pendingIds = await _read.UserNotifications
            .AsNoTracking()
            .Where(n => n.LiveDeliveredAtUtc == null && n.CreatedAtUtc >= cutoff)
            .OrderBy(n => n.CreatedAtUtc)
            .Take(batchSize)
            .Select(n => n.Id)
            .ToListAsync(cancellationToken);

        int delivered = 0;
        foreach (Guid id in pendingIds)
        {
            if (await _processor.TryDeliverAsync(id, cancellationToken))
                delivered++;
        }

        return delivered;
    }
}
