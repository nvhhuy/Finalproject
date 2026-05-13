using Application.Contracts.Modules.Users.Persistence;
using Domains.UserAggregate;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Users.Profile.Commands;

public sealed record UpdateMyProfileCommand(Guid UserId, string FirstName, string LastName) : IRequest<Result<bool, Error>>;

public sealed class UpdateMyProfileCommandHandler : IRequestHandler<UpdateMyProfileCommand, Result<bool, Error>>
{
    private readonly IUserRespository _users;

    public UpdateMyProfileCommandHandler(IUserRespository users) => _users = users;

    public async Task<Result<bool, Error>> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        User? user = await _users.GetUserByIdForUpdateAsync(request.UserId, cancellationToken);
        if (user is null)
            return new NotFoundError("User was not found.");

        user.UpdateProfile(request.FirstName, request.LastName, user.UserName, user.Email);
        await _users.SaveChangesAsync(cancellationToken);
        return true;
    }
}
