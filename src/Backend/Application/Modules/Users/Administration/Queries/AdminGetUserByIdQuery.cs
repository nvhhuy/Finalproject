using Application.Contracts.Authorization;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Mapping;

namespace Application.Modules.Users.Administration.Queries;

public sealed record AdminGetUserByIdQuery(Guid UserId)
    : IRequest<Result<AdminUserDetailDto, Error>>, IRequireAdmin;

public sealed record AdminUserDetailDto(
    Guid Id,
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsLocked,
    string? LockReason,
    bool TwoFactorEnabled,
    DateTime? EmailVerifiedAtUtc,
    DateTime? RegisteredAtUtc,
    DateTime? LastLoginAtUtc,
    DateTime? DeletedAtUtc,
    DateTime? AccountClosedAtUtc,
    uint RowVersion);

public sealed class AdminGetUserByIdQueryHandler
    : IRequestHandler<AdminGetUserByIdQuery, Result<AdminUserDetailDto, Error>>
{
    private readonly IUserReadRepository _users;

    public AdminGetUserByIdQueryHandler(IUserReadRepository users)
    {
        _users = users;
    }

    public async Task<Result<AdminUserDetailDto, Error>> Handle(
        AdminGetUserByIdQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId, cancellationToken: cancellationToken);
        if (user is null || user.DeletedAtUtc is not null)
            return new NotFoundError("User was not found.");

        return user.ToAdminDetailDto();
    }
}