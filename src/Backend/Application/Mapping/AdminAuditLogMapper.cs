using Application.Modules.Users.Administration.Queries;
using Domains.Administration;
using Riok.Mapperly.Abstractions;

namespace Application.Mapping;

[Mapper]
public static partial class AdminAuditLogMapper
{
    public static partial AdminAuditLogEntryDto ToEntryDto(this AdminAuditLog log);

    public static partial IReadOnlyList<AdminAuditLogEntryDto> ToEntryDtos(this IEnumerable<AdminAuditLog> logs);
}
