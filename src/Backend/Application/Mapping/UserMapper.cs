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

    public static partial IReadOnlyList<AdminUserSummaryDto> ToAdminSummaryDtos(this IEnumerable<User> users);

    public static UserProfileDto ToProfileDto(this User user) =>
        new(
            user.Id.Value,
            user.UserName,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role,
            user.TwoFactorEnabled,
            user.EmailVerifiedAtUtc,
            user.AvatarMediaAssetId,
            null,
            user.RowVersion);
}