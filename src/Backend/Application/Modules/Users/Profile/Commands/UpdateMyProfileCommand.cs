using Application.Common;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Transactions;

namespace Application.Modules.Users.Profile.Commands;

public sealed record UpdateMyProfileCommand(Guid UserId, string FirstName, string LastName, uint ExpectedRowVersion)
    : IRequest<Result<bool, Error>>;

public sealed class UpdateMyProfileCommandHandler : IRequestHandler<UpdateMyProfileCommand, Result<bool, Error>>
{
    private readonly IUnitOfWork _uow;
    private readonly IUserWriteRepository _users;

    public UpdateMyProfileCommandHandler(IUserWriteRepository users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdForUpdateAsync(request.UserId, cancellationToken);
        if (user is null)
            return new NotFoundError("User was not found.");

        var conflict = EntityConcurrency.ValidateExpectedVersion(user, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        user.UpdateProfile(request.FirstName, request.LastName, user.UserName, user.Email);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}