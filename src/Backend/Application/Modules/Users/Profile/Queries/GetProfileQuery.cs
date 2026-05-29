using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Mapping;

namespace Application.Modules.Users.Profile.Queries;

public sealed record GetProfileQuery(Guid UserId) : IRequest<Result<UserProfileDto, Error>>;

public sealed record UserProfileDto(
    Guid Id,
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool TwoFactorEnabled,
    uint RowVersion);

public sealed class GetProfileQueryHandler : IRequestHandler<GetProfileQuery, Result<UserProfileDto, Error>>
{
    private readonly IUserReadRepository _users;

    public GetProfileQueryHandler(IUserReadRepository users)
    {
        _users = users;
    }

    public async Task<Result<UserProfileDto, Error>> Handle(GetProfileQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId, cancellationToken: cancellationToken);
        if (user is null)
            return new NotFoundError("User was not found.");

        return user.ToProfileDto();
    }
}