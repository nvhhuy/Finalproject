using Application.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Application.Modules.Notifications.Queries;

public sealed record NotificationDto(
    Guid Id,
    string Title,
    string Body,
    string? LinkUrl,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

public sealed record ListUserNotificationsQuery(Guid UserId, int Page = 1, int PageSize = 20)
    : IRequest<Result<IReadOnlyList<NotificationDto>, Error>>;

public sealed class ListUserNotificationsQueryHandler
    : IRequestHandler<ListUserNotificationsQuery, Result<IReadOnlyList<NotificationDto>, Error>>
{
    private readonly IApplicationReadDbContext _read;

    public ListUserNotificationsQueryHandler(IApplicationReadDbContext read)
    {
        _read = read;
    }

    public async Task<Result<IReadOnlyList<NotificationDto>, Error>> Handle(
        ListUserNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var size = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var items = await _read.UserNotifications
            .AsNoTracking()
            .Where(n => n.UserId == request.UserId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(n => new NotificationDto(
                n.Id,
                n.Title,
                n.Body,
                n.LinkUrl,
                n.CreatedAtUtc,
                n.ReadAtUtc))
            .ToListAsync(cancellationToken);

        return items;
    }
}