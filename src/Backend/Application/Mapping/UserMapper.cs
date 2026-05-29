using Application.Modules.Users.Administration.Queries;
using Application.Modules.Users.Profile.Queries;
using Domains.UserAggregate;
using Riok.Mapperly.Abstractions;

namespace Application.Mapping;

[Mapper]
[UseStaticMapper(typeof(MappingConversions))]
public static partial class UserMapper
{
    public static partial AdminUserSummaryDto ToAdminSummaryDto(this User user);

    public static partial AdminUserDetailDto ToAdminDetailDto(this User user);

    public static partial UserProfileDto ToProfileDto(this User user);

    public static partial IReadOnlyList<AdminUserSummaryDto> ToAdminSummaryDtos(this IEnumerable<User> users);
}