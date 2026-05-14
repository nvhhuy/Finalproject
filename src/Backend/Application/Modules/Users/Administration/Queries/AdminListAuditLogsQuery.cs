using Application.Common;
using Application.Contracts.Modules.Users.Persistence;
using Domains.Administration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Common;
using Shared.Errors;

namespace Application.Modules.Users.Administration.Queries;

public sealed record AdminListAuditLogsQuery(int Page, int PageSize, Guid? TargetUserId = null)
    : IRequest<Result<AdminAuditLogListDto, Error>>;

public sealed record AdminAuditLogEntryDto(
    Guid Id,
    Guid ActorUserId,
    string Action,
    Guid? TargetUserId,
    string? IpAddress,
    string? Details,
    DateTime CreatedAtUtc);

public sealed record AdminAuditLogListDto(IReadOnlyList<AdminAuditLogEntryDto> Items, int TotalCount, int Page, int PageSize);

public sealed class AdminListAuditLogsQueryHandler : IRequestHandler<AdminListAuditLogsQuery, Result<AdminAuditLogListDto, Error>>
{
    private readonly IApplicationReadDbContext _read;

    public AdminListAuditLogsQueryHandler(IApplicationReadDbContext read) => _read = read;

    public async Task<Result<AdminAuditLogListDto, Error>> Handle(AdminListAuditLogsQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.PageSize < 1 || request.PageSize > 200)
            return new Error("BadRequest", 400, "Page must be at least 1 and page size between 1 and 200.");

        IQueryable<AdminAuditLog> query = _read.AdminAuditLogs.AsNoTracking().OrderByDescending(a => a.CreatedAtUtc);
        if (request.TargetUserId is not null)
            query = query.Where(a => a.TargetUserId == request.TargetUserId);

        int total = await query.CountAsync(cancellationToken);
        List<AdminAuditLog> rows = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        AdminAuditLogEntryDto[] items = rows
            .Select(a => new AdminAuditLogEntryDto(a.Id, a.ActorUserId, a.Action, a.TargetUserId, a.IpAddress, a.Details, a.CreatedAtUtc))
            .ToArray();

        return new AdminAuditLogListDto(items, total, request.Page, request.PageSize);
    }
}
