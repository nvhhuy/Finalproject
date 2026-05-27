using Application.Common;
using Application.Contracts.Administration;
using Application.Contracts.Authorization;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Application.Modules.Users.Authentication.Common;
using Domains.UserAggregate;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.Authentication;
using Shared.Errors.General;

namespace Application.Modules.Users.Administration.Commands;

public sealed record ImpersonateUserCommand(
    Guid TargetUserId,
    Guid ActingAdminUserId,
    uint ExpectedRowVersion,
    string? RemoteIpAddress = null)
    : IRequest<Result<AuthenticationResponse, Error>>, IRequireAdmin;

public sealed class ImpersonateUserCommandHandler : IRequestHandler<ImpersonateUserCommand, Result<AuthenticationResponse, Error>>
{
    private readonly IUserReadRepository _read;
    private readonly IAuthenticationTokenIssuer _tokens;
    private readonly IAdminAuditLogWriter _audit;
    private readonly IUnitOfWork _uow;

    public ImpersonateUserCommandHandler(IUserReadRepository read, IAuthenticationTokenIssuer tokens, IAdminAuditLogWriter audit, IUnitOfWork uow)
    {
        _read = read;
        _tokens = tokens;
        _audit = audit;
        _uow = uow;
    }

    public async Task<Result<AuthenticationResponse, Error>> Handle(ImpersonateUserCommand request, CancellationToken cancellationToken)
    {
        if (request.TargetUserId == request.ActingAdminUserId)
            return new ForbiddenError("Administrators cannot impersonate themselves.");

        User? target = await _read.GetByIdAsync(request.TargetUserId, cancellationToken: cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        Error? conflict = EntityConcurrency.ValidateExpectedVersion(target, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        if (string.Equals(target.Role, UserRoles.Admin, StringComparison.Ordinal))
            return new ForbiddenError("Impersonating another administrator is not allowed.");

        if (!target.CanSignIn)
            return new AccountDisabledError("The target user account is locked, closed, or deleted.");

        Result<AuthenticationTokenPair, Error> pair = await _tokens.IssueAsync(
            target,
            request.RemoteIpAddress,
            cancellationToken,
            impersonatorUserId: request.ActingAdminUserId);

        if (pair.IsFailure)
            return pair.Error;

        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserImpersonated, request.TargetUserId, request.RemoteIpAddress, null);
        await _uow.SaveChangesAsync(cancellationToken);

        return new AuthenticationResponse(
            target,
            pair.Value.AccessToken,
            pair.Value.AccessTokenExpiresUtc,
            pair.Value.RefreshToken,
            pair.Value.RefreshTokenExpiresUtc);
    }
}
