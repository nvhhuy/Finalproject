using Application.Contracts.Authorization;
using Application.Contracts.Persistence;
using Domains.Support;
using Domains.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Application.Modules.Users.Administration.Queries;

public sealed record AdminDashboardMetricsQuery : IRequest<Result<AdminDashboardMetricsDto, Error>>, IRequireAdmin;

public sealed record AdminDashboardMetricsDto(
    int TotalActiveUsers,
    int LockedUsers,
    int RegistrationsToday,
    int RegistrationsLast7Days,
    int TotalTenants,
    int OpenSupportTickets,
    int UnreadContactMessages,
    IReadOnlyList<DailyRegistrationDto> RegistrationsLast14Days);

public sealed record DailyRegistrationDto(DateOnly Day, int Count);

public sealed class
    AdminDashboardMetricsQueryHandler : IRequestHandler<AdminDashboardMetricsQuery,
    Result<AdminDashboardMetricsDto, Error>>
{
    private readonly IApplicationReadDbContext _read;

    public AdminDashboardMetricsQueryHandler(IApplicationReadDbContext read)
    {
        _read = read;
    }

    public async Task<Result<AdminDashboardMetricsDto, Error>> Handle(AdminDashboardMetricsQuery request,
        CancellationToken cancellationToken)
    {
        var total = await _read.Users.AsNoTracking().CountAsync(u => u.DeletedAtUtc == null, cancellationToken);

        var fromUtc = DateTime.UtcNow.Date.AddDays(-13);
        var dailyCounts = await _read.Users.AsNoTracking()
            .Where(u => u.DeletedAtUtc == null && u.RegisteredAtUtc != null && u.RegisteredAtUtc >= fromUtc)
            .GroupBy(u => u.RegisteredAtUtc!.Value.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .OrderBy(x => x.Day)
            .ToListAsync(cancellationToken);
        var daily = dailyCounts
            .Select(x => new DailyRegistrationDto(DateOnly.FromDateTime(x.Day), x.Count))
            .ToList();

        var start = DateOnly.FromDateTime(fromUtc);
        var end = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var byDay = daily.ToDictionary(x => x.Day, x => x.Count);
        var filled = new List<DailyRegistrationDto>();
        for (var d = start; d <= end; d = d.AddDays(1))
            filled.Add(new DailyRegistrationDto(d, byDay.TryGetValue(d, out var c) ? c : 0));

        var today = end;
        var weekStart = today.AddDays(-6);
        var registrationsToday = byDay.TryGetValue(today, out var todayCount) ? todayCount : 0;
        var registrationsLast7 = filled.Where(x => x.Day >= weekStart).Sum(x => x.Count);

        var locked = await _read.Users.AsNoTracking()
            .CountAsync(u => u.DeletedAtUtc == null && u.IsLocked, cancellationToken);
        var tenants = await _read.Tenants.AsNoTracking()
            .CountAsync(t => t.Status != TenantStatus.Deleted, cancellationToken);
        var openTickets = await _read.SupportTickets.AsNoTracking()
            .CountAsync(
                t => t.Status != SupportTicketStatus.Resolved && t.Status != SupportTicketStatus.Closed,
                cancellationToken);
        var unreadContact = await _read.ContactMessages.AsNoTracking()
            .CountAsync(m => !m.IsRead, cancellationToken);

        return new AdminDashboardMetricsDto(
            total,
            locked,
            registrationsToday,
            registrationsLast7,
            tenants,
            openTickets,
            unreadContact,
            filled);
    }
}