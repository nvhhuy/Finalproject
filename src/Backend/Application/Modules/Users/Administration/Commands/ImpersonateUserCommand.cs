using Application.Contracts.Administration;
using Application.Contracts.Modules.Users.Persistence;
using Application.Contracts.Services;
using Application.Modules.Users.Authentication.Common;
using Domains.UserAggregate;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Users.Administration.Commands;

public sealed record ImpersonateUserCommand(Guid TargetUserId, Guid ActingAdminUserId, string? RemoteIpAddress = null)
    : IRequest<Result<AuthenticationResponse, Error>>;

public sealed class ImpersonateUserCommandHandler : IRequestHandler<ImpersonateUserCommand, Result<AuthenticationResponse, Error>>
{
    private readonly IUserRespository _users;
    private readonly IAuthenticationTokenIssuer _tokens;
    private readonly IAdminAuditLogWriter _audit;

    public ImpersonateUserCommandHandler(IUserRespository users, IAuthenticationTokenIssuer tokens, IAdminAuditLogWriter audit)
    {
        _users = users;
        _tokens = tokens;
        _audit = audit;
    }

    public async Task<Result<AuthenticationResponse, Error>> Handle(ImpersonateUserCommand request, CancellationToken cancellationToken)
    {
        User? admin = await _users.GetUserByIdAsync(request.ActingAdminUserId, cancellationToken);
        if (admin is null || admin.Role != UserRoles.Admin)
            return new ForbiddenError("Administrator privileges are required.");

        User? target = await _users.GetUserByIdAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        Result<AuthenticationTokenPair, Error> pair = await _tokens.IssueAsync(
            target,
            request.RemoteIpAddress,
            cancellationToken,
            impersonatorUserId: request.ActingAdminUserId);

        if (pair.IsFailure)
            return pair.Error;

        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserImpersonated, request.TargetUserId, request.RemoteIpAddress, null);
        await _users.SaveChangesAsync(cancellationToken);

        return new AuthenticationResponse(
            target,
            pair.Value.AccessToken,
            pair.Value.AccessTokenExpiresUtc,
            pair.Value.RefreshToken,
            pair.Value.RefreshTokenExpiresUtc);
    }
}
