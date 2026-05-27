using Application.Contracts.Authorization;
using Application.Contracts.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Common;
using Shared.Errors;

namespace Application.Modules.Users.Administration.Queries;

public sealed record AdminDashboardMetricsQuery : IRequest<Result<AdminDashboardMetricsDto, Error>>, IRequireAdmin;

public sealed record AdminDashboardMetricsDto(int TotalActiveUsers, IReadOnlyList<DailyRegistrationDto> RegistrationsLast14Days);

public sealed record DailyRegistrationDto(DateOnly Day, int Count);

public sealed class AdminDashboardMetricsQueryHandler : IRequestHandler<AdminDashboardMetricsQuery, Result<AdminDashboardMetricsDto, Error>>
{
    private readonly IApplicationReadDbContext _read;

    public AdminDashboardMetricsQueryHandler(IApplicationReadDbContext read) => _read = read;

    public async Task<Result<AdminDashboardMetricsDto, Error>> Handle(AdminDashboardMetricsQuery request, CancellationToken cancellationToken)
    {
        int total = await _read.Users.AsNoTracking().CountAsync(u => u.DeletedAtUtc == null, cancellationToken);

        DateTime fromUtc = DateTime.UtcNow.Date.AddDays(-13);
        var dailyCounts = await _read.Users.AsNoTracking()
            .Where(u => u.DeletedAtUtc == null && u.RegisteredAtUtc != null && u.RegisteredAtUtc >= fromUtc)
            .GroupBy(u => u.RegisteredAtUtc!.Value.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .OrderBy(x => x.Day)
            .ToListAsync(cancellationToken);
        List<DailyRegistrationDto> daily = dailyCounts
            .Select(x => new DailyRegistrationDto(DateOnly.FromDateTime(x.Day), x.Count))
            .ToList();

        DateOnly start = DateOnly.FromDateTime(fromUtc);
        DateOnly end = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var byDay = daily.ToDictionary(x => x.Day, x => x.Count);
        var filled = new List<DailyRegistrationDto>();
        for (DateOnly d = start; d <= end; d = d.AddDays(1))
            filled.Add(new DailyRegistrationDto(d, byDay.TryGetValue(d, out int c) ? c : 0));

        return new AdminDashboardMetricsDto(total, filled);
    }
}
