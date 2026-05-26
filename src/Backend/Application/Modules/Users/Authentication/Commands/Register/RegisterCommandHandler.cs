using Application.Contracts.Email;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Services;
using Application.Contracts.Settings;
using Application.Contracts.Transactions;
using Application.Extensions;
using Application.Modules.SystemSettings;
using Application.Modules.Users.Authentication.Common;
using Application.Modules.Users.Profile.DomainEvents;
using Domains.UserAggregate;
using Microsoft.AspNetCore.Identity;
using Shared.Errors;
using Shared.Errors.Authentication;
using Shared.Errors.General;

namespace Application.Modules.Users.Authentication.Commands.Register;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<RegistrationResult, Error>>
{
    private readonly IUserReadRepository _userRead;
    private readonly IUserWriteRepository _userWrite;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IAppSettingsService _settings;
    private readonly IDateTimeProvider _clock;
    private readonly IEmailVerificationSender _emailVerification;
    private readonly IUnitOfWork _uow;

    public RegisterCommandHandler(
        IUserReadRepository userRead,
        IUserWriteRepository userWrite,
        IPasswordHasher<User> passwordHasher,
        IAppSettingsService settings,
        IDateTimeProvider clock,
        IEmailVerificationSender emailVerification,
        IUnitOfWork uow)
    {
        _userRead = userRead;
        _userWrite = userWrite;
        _passwordHasher = passwordHasher;
        _settings = settings;
        _clock = clock;
        _emailVerification = emailVerification;
        _uow = uow;
    }

    public async Task<Result<RegistrationResult, Error>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        bool allowPublicRegister = await _settings.GetBooleanAsync(AppSettingKeys.AllowPublicRegister, defaultValue: true, cancellationToken);
        if (!allowPublicRegister)
            return new ForbiddenError("Public registration is disabled by application setting.");

        if (await _userRead.ExistsByEmailAsync(request.Email, cancellationToken))
            return new EmailAlreadyInUseError();

        if (await _userRead.ExistsByUserNameAsync(request.UserName, cancellationToken))
            return new UserNameAlreadyTakenError();

        User user = User.CreateForLocalLoginPendingHash(request.FirstName, request.LastName, request.UserName, request.Email);
        user.SetInitialPasswordHash(_passwordHasher.HashPassword(user, request.Password));
        user.MarkRegisteredAt(_clock.Now);

        user.AddDomainEvent(new UserRegisteredInTransactionEvent(user.Id.Value, user.Email));
        user.AddDomainEvent(new UserRegisteredAfterCommitEvent(user.Id.Value));

        _userWrite.AddUser(user);
        await _uow.SaveChangesAsync(cancellationToken);
        await _userWrite.InvalidateUserCacheAsync(user.Id.Value, user.Email, user.UserName, cancellationToken);

        await _emailVerification.SendVerificationEmailAsync(user.Id.Value, user.Email, cancellationToken);

        return new RegistrationResult(
            user.Email,
            "Registration successful. Please check your email to verify your account before signing in.");
    }
}
