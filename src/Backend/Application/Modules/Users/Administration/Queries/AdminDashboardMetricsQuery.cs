using Application.Contracts.Modules.Users.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Common;
using Shared.Errors;

namespace Application.Modules.Users.Administration.Queries;

public sealed record AdminDashboardMetricsQuery : IRequest<Result<AdminDashboardMetricsDto, Error>>;

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
        List<DailyRegistrationDto> daily = await _read.Users.AsNoTracking()
            .Where(u => u.DeletedAtUtc == null && u.RegisteredAtUtc != null && u.RegisteredAtUtc >= fromUtc)
            .GroupBy(u => DateOnly.FromDateTime(u.RegisteredAtUtc!.Value.Date))
            .Select(g => new DailyRegistrationDto(g.Key, g.Count()))
            .OrderBy(x => x.Day)
            .ToListAsync(cancellationToken);

        DateOnly start = DateOnly.FromDateTime(fromUtc);
        DateOnly end = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var byDay = daily.ToDictionary(x => x.Day, x => x.Count);
        var filled = new List<DailyRegistrationDto>();
        for (DateOnly d = start; d <= end; d = d.AddDays(1))
            filled.Add(new DailyRegistrationDto(d, byDay.TryGetValue(d, out int c) ? c : 0));

        return new AdminDashboardMetricsDto(total, filled);
    }
}
