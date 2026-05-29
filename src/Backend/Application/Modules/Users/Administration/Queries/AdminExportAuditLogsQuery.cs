using System.Globalization;
using System.Text;
using Application.Contracts.Authorization;
using Application.Contracts.Persistence;
using Domains.Administration;
using Microsoft.EntityFrameworkCore;

namespace Application.Modules.Users.Administration.Queries;

public sealed record AdminExportAuditLogsQuery(
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? TargetUserId,
    int MaxRows = 10_000) : IRequest<Result<AdminAuditExportDto, Error>>, IRequireAdmin;

public sealed record AdminAuditExportDto(byte[] Content, string FileName, string ContentType);

public sealed class AdminExportAuditLogsQueryHandler
    : IRequestHandler<AdminExportAuditLogsQuery, Result<AdminAuditExportDto, Error>>
{
    private readonly IApplicationReadDbContext _read;

    public AdminExportAuditLogsQueryHandler(IApplicationReadDbContext read)
    {
        _read = read;
    }

    public async Task<Result<AdminAuditExportDto, Error>> Handle(
        AdminExportAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        var maxRows = Math.Clamp(request.MaxRows, 1, 50_000);
        if (request.FromUtc is not null && request.ToUtc is not null && request.FromUtc > request.ToUtc)
            return new ValidationError("dateRange", "FromUtc must be before ToUtc.");

        IQueryable<AdminAuditLog> query = _read.AdminAuditLogs.AsNoTracking().OrderByDescending(a => a.CreatedAtUtc);
        if (request.TargetUserId is not null)
            query = query.Where(a => a.TargetUserId == request.TargetUserId);
        if (request.FromUtc is not null)
            query = query.Where(a => a.CreatedAtUtc >= request.FromUtc.Value);
        if (request.ToUtc is not null)
            query = query.Where(a => a.CreatedAtUtc <= request.ToUtc.Value);

        var rows = await query.Take(maxRows).ToListAsync(cancellationToken);
        var csv = BuildCsv(rows);
        var fileName = $"admin-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}Z.csv";
        return new AdminAuditExportDto(Encoding.UTF8.GetBytes(csv), fileName, "text/csv; charset=utf-8");
    }

    private static string BuildCsv(IReadOnlyList<AdminAuditLog> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("id,createdAtUtc,action,actorUserId,targetUserId,ipAddress,details");
        foreach (var row in rows)
        {
            sb.Append(Csv(row.Id.ToString()));
            sb.Append(',');
            sb.Append(Csv(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)));
            sb.Append(',');
            sb.Append(Csv(row.Action));
            sb.Append(',');
            sb.Append(Csv(row.ActorUserId.ToString()));
            sb.Append(',');
            sb.Append(Csv(row.TargetUserId?.ToString()));
            sb.Append(',');
            sb.Append(Csv(row.IpAddress));
            sb.Append(',');
            sb.Append(Csv(row.Details));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}