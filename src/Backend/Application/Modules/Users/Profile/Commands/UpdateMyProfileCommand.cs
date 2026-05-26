using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Transactions;
using Domains.UserAggregate;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Users.Profile.Commands;

public sealed record UpdateMyProfileCommand(Guid UserId, string FirstName, string LastName) : IRequest<Result<bool, Error>>;

public sealed class UpdateMyProfileCommandHandler : IRequestHandler<UpdateMyProfileCommand, Result<bool, Error>>
{
    private readonly IUserWriteRepository _users;
    private readonly IUnitOfWork _uow;

    public UpdateMyProfileCommandHandler(IUserWriteRepository users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdForUpdateAsync(request.UserId, cancellationToken);
        if (user is null)
            return new NotFoundError("User was not found.");

        user.UpdateProfile(request.FirstName, request.LastName, user.UserName, user.Email);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}
