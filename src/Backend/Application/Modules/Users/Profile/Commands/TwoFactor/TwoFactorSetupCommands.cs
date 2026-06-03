using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Transactions;
using OtpNet;
using Shared.Errors.Authentication;

namespace Application.Modules.Users.Profile.Commands.TwoFactor;

public sealed record TwoFactorSetupResponse(string SharedKey, string OtpAuthUri);

public sealed record BeginTwoFactorSetupCommand(Guid UserId) : IRequest<Result<TwoFactorSetupResponse, Error>>;

public sealed class
    BeginTwoFactorSetupCommandHandler : IRequestHandler<BeginTwoFactorSetupCommand,
    Result<TwoFactorSetupResponse, Error>>
{
    private readonly IUnitOfWork _uow;
    private readonly IUserWriteRepository _users;

    public BeginTwoFactorSetupCommandHandler(IUserWriteRepository users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<Result<TwoFactorSetupResponse, Error>> Handle(BeginTwoFactorSetupCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdForUpdateAsync(request.UserId, cancellationToken);
        if (user is null)
            return new InvalidCredentialsError("User was not found.");

        var secret = KeyGeneration.GenerateRandomKey(20);
        var base32 = Base32Encoding.ToString(secret);
        user.SetTwoFactorPendingSecret(base32);
        await _uow.SaveChangesAsync(cancellationToken);

        const string issuer = "BaseProject";
        var uri =
            $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(user.Email)}?secret={base32}&issuer={Uri.EscapeDataString(issuer)}";

        return new TwoFactorSetupResponse(base32, uri);
    }
}

public sealed record ConfirmTwoFactorSetupCommand(Guid UserId, string Code) : IRequest<Result<bool, Error>>;

public sealed class
    ConfirmTwoFactorSetupCommandHandler : IRequestHandler<ConfirmTwoFactorSetupCommand, Result<bool, Error>>
{
    private readonly IUnitOfWork _uow;
    private readonly IUserWriteRepository _users;

    public ConfirmTwoFactorSetupCommandHandler(IUserWriteRepository users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(ConfirmTwoFactorSetupCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdForUpdateAsync(request.UserId, cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.TwoFactorSecret))
            return new InvalidCredentialsError("Two-factor setup was not started.");

        var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));
        if (!totp.VerifyTotp(request.Code, out _, new VerificationWindow(1, 1)))
            return new InvalidCredentialsError("Invalid authenticator code.");

        user.ActivateTwoFactor();
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed record DisableTwoFactorCommand(Guid UserId, string Code) : IRequest<Result<bool, Error>>;

public sealed class DisableTwoFactorCommandHandler : IRequestHandler<DisableTwoFactorCommand, Result<bool, Error>>
{
    private readonly IUnitOfWork _uow;
    private readonly IUserWriteRepository _users;

    public DisableTwoFactorCommandHandler(IUserWriteRepository users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(DisableTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdForUpdateAsync(request.UserId, cancellationToken);
        if (user is null)
            return new InvalidCredentialsError("User was not found.");

        if (!user.TwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
            return new ValidationError("code", "Two-factor authentication is not enabled.");

        var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));
        if (!totp.VerifyTotp(request.Code, out _, new VerificationWindow(1, 1)))
            return new InvalidCredentialsError("Invalid authenticator code.");

        user.DisableTwoFactor();
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}