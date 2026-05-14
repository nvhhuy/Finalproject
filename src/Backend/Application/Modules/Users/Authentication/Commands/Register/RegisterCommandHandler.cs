using Application.Modules.Users.Authentication.Common;
using Application.Contracts.Modules.Users.Persistence;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Services;
using Application.Extensions;
using Application.Contracts.Settings;
using Application.Modules.SystemSettings;
using Application.Modules.Users.Profile.DomainEvents;
using Domains.UserAggregate;
using Microsoft.AspNetCore.Identity;
using Shared.Errors;
using Shared.Errors.Authentication;
using Shared.Errors.General;

namespace Application.Modules.Users.Authentication.Commands.Register;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthenticationResponse, Error>>
{
    private readonly IUserReadRepository _userRead;
    private readonly IUserRespository _users;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IAuthenticationTokenIssuer _tokens;
    private readonly IAppSettingsService _settings;
    private readonly IDateTimeProvider _clock;

    public RegisterCommandHandler(
        IUserReadRepository userRead,
        IUserRespository users,
        IPasswordHasher<User> passwordHasher,
        IAuthenticationTokenIssuer tokens,
        IAppSettingsService settings,
        IDateTimeProvider clock)
    {
        _userRead = userRead;
        _users = users;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _settings = settings;
        _clock = clock;
    }

    public async Task<Result<AuthenticationResponse, Error>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        Result<bool, ValidationError> validationResult = await request.ValidateAsync(new RegisterCommandValidator(), cancellationToken);
        if (validationResult.IsFailure)
            return validationResult.Error;

        bool allowPublicRegister = await _settings.GetBooleanAsync(AppSettingKeys.AllowPublicRegister, defaultValue: true, cancellationToken);
        if (!allowPublicRegister)
            return new ForbiddenError("Public registration is disabled by application setting.");

        if (await _userRead.ExistsByEmailAsync(request.Email, cancellationToken))
            return new EmailAlreadyInUseError();

        if (await _userRead.ExistsByUserNameAsync(request.UserName, cancellationToken))
            return new UserNameAlreadyTakenError();

        User user = User.CreateNew(request.FirstName, request.LastName, request.UserName, request.Email, request.Password);
        string hashedPassword = _passwordHasher.HashPassword(user, request.Password);
        user.UpdatePassword(hashedPassword);
        user.MarkRegisteredAt(_clock.Now);

        user.AddDomainEvent(new UserRegisteredInTransactionEvent(user.Id.Value, user.Email));
        user.AddDomainEvent(new UserRegisteredAfterCommitEvent(user.Id.Value));

        _users.AddUser(user);
        await _users.SaveChangesAsync(cancellationToken);
        await _users.InvalidateUserCacheAsync(user.Id.Value, user.Email, user.UserName, cancellationToken);

        Result<AuthenticationTokenPair, Error> pair = await _tokens.IssueAsync(user, request.RemoteIpAddress, cancellationToken);
        if (pair.IsFailure)
            return pair.Error;

        return new AuthenticationResponse(
            user,
            pair.Value.AccessToken,
            pair.Value.AccessTokenExpiresUtc,
            pair.Value.RefreshToken,
            pair.Value.RefreshTokenExpiresUtc);
    }
}
