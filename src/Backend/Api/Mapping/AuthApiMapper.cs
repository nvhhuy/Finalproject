using Api.Models.Auth;
using Application.Mapping;
using Application.Modules.Users.Authentication.Common;
using Domains.UserAggregate.ValueObjects;
using Riok.Mapperly.Abstractions;

namespace Api.Mapping;

[Mapper]
public static partial class AuthApiMapper
{
    [UserMapping(Default = true)]
    private static Guid ToGuid(UserId id) => MappingConversions.ToGuid(id);

    [MapProperty("User.Id", nameof(AuthResponseDto.UserId))]
    [MapProperty("User.UserName", nameof(AuthResponseDto.UserName))]
    [MapProperty("User.Email", nameof(AuthResponseDto.Email))]
    [MapProperty("User.FirstName", nameof(AuthResponseDto.FirstName))]
    [MapProperty("User.LastName", nameof(AuthResponseDto.LastName))]
    public static partial AuthResponseDto ToAuthResponseDto(this AuthenticationResponse response);
}
