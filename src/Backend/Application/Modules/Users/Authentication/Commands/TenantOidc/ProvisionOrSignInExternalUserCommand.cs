using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Tenancy;
using Application.Contracts.Transactions;
using Application.Modules.Users.Authentication.Common;
using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Shared.Errors.Authentication;

namespace Application.Modules.Users.Authentication.Commands.TenantOidc;

public sealed record ProvisionOrSignInExternalUserCommand(
    Guid TenantId,
    string ProviderSubject,
    string Email,
    string? GivenName,
    string? FamilyName,
    string? RemoteIpAddress = null) : IRequest<Result<AuthenticationResponse, Error>>;

public sealed class ProvisionOrSignInExternalUserCommandHandler
    : IRequestHandler<ProvisionOrSignInExternalUserCommand, Result<AuthenticationResponse, Error>>
{
    private readonly IDateTimeProvider _clock;
    private readonly ITenantMembershipService _membership;
    private readonly IApplicationReadDbContext _read;
    private readonly ITenantStore _tenants;
    private readonly IAuthenticationTokenIssuer _tokens;
    private readonly IUnitOfWork _uow;
    private readonly IUserWriteRepository _users;

    public ProvisionOrSignInExternalUserCommandHandler(
        IApplicationReadDbContext read,
        IUserWriteRepository users,
        ITenantStore tenants,
        ITenantMembershipService membership,
        IAuthenticationTokenIssuer tokens,
        IDateTimeProvider clock,
        IUnitOfWork uow)
    {
        _read = read;
        _users = users;
        _tenants = tenants;
        _membership = membership;
        _tokens = tokens;
        _clock = clock;
        _uow = uow;
    }

    public async Task<Result<AuthenticationResponse, Error>> Handle(
        ProvisionOrSignInExternalUserCommand request,
        CancellationToken cancellationToken)
    {
        var provider = ExternalLoginProviders.TenantOidc(request.TenantId);
        var link = await _read.ExternalLogins
            .FirstOrDefaultAsync(
                e => e.Provider == provider && e.ProviderSubject == request.ProviderSubject,
                cancellationToken);

        User? user;
        if (link is not null)
        {
            user = await _read.Users.FirstOrDefaultAsync(u => u.Id == link.UserId, cancellationToken);
            if (user is null)
                return new InvalidCredentialsError("Linked user was not found.");
        }
        else
        {
            user = await _read.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);
            if (user is null)
            {
                var userName = await BuildUniqueUserNameAsync(request.Email, cancellationToken);
                var first = request.GivenName ?? request.FamilyName ?? "User";
                var last = request.FamilyName ?? string.Empty;
                user = User.CreateForExternal(first, last, userName, request.Email);
                user.MarkRegisteredAt(_clock.Now);
                user.MarkEmailVerified(_clock.Now);
                _users.AddUser(user);
                _users.AddExternalLogin(ExternalUserLogin.Create(user.Id, provider, request.ProviderSubject));
            }
            else
            {
                var alreadyLinked = await _read.ExternalLogins.AnyAsync(
                    e => e.UserId.Value == user.Id.Value
                         && e.Provider == provider
                         && e.ProviderSubject == request.ProviderSubject,
                    cancellationToken);
                if (!alreadyLinked)
                    _users.AddExternalLogin(ExternalUserLogin.Create(user.Id, provider, request.ProviderSubject));
            }
        }

        await EnsureTenantMemberAsync(request.TenantId, user.Id.Value, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        await _users.InvalidateUserCacheAsync(user.Id.Value, user.Email, user.UserName, cancellationToken);

        if (!user.CanSignIn)
            return new AccountDisabledError("This account cannot sign in.");

        var pair = await _tokens.IssueAsync(user, request.RemoteIpAddress, cancellationToken);
        if (pair.IsFailure)
            return pair.Error;

        return new AuthenticationResponse(
            user,
            pair.Value.AccessToken,
            pair.Value.AccessTokenExpiresUtc,
            pair.Value.RefreshToken,
            pair.Value.RefreshTokenExpiresUtc);
    }

    private async Task EnsureTenantMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        if (await _membership.IsMemberAsync(tenantId, userId, cancellationToken))
            return;

        var tenantKey = TenantId.Create(tenantId);
        var tenant = await _tenants.GetByIdForUpdateAsync(tenantKey, cancellationToken);
        if (tenant is null)
            return;

        try
        {
            tenant.AddMember(UserId.Create(userId), TenantMemberRoles.Member, _clock.Now);
        }
        catch (InvalidOperationException)
        {
            // Already a member (race).
        }
    }

    private async Task<string> BuildUniqueUserNameAsync(string email, CancellationToken cancellationToken)
    {
        var local = email.Split('@')[0];
        var candidate = local;
        var i = 0;
        while (await _read.Users.AnyAsync(u => u.UserName == candidate, cancellationToken))
        {
            i++;
            candidate = $"{local}{i}";
        }

        return candidate;
    }
}