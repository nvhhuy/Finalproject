using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Transactions;
using Domains.UserAggregate;
using MediatR;
using OtpNet;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.Authentication;

namespace Application.Modules.Users.Profile.Commands.TwoFactor;

public sealed record TwoFactorSetupResponse(string SharedKey, string OtpAuthUri);

public sealed record BeginTwoFactorSetupCommand(Guid UserId) : IRequest<Result<TwoFactorSetupResponse, Error>>;

public sealed class BeginTwoFactorSetupCommandHandler : IRequestHandler<BeginTwoFactorSetupCommand, Result<TwoFactorSetupResponse, Error>>
{
    private readonly IUserWriteRepository _users;
    private readonly IUnitOfWork _uow;

    public BeginTwoFactorSetupCommandHandler(IUserWriteRepository users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<Result<TwoFactorSetupResponse, Error>> Handle(BeginTwoFactorSetupCommand request, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdForUpdateAsync(request.UserId, cancellationToken);
        if (user is null)
            return new InvalidCredentialsError("User was not found.");

        byte[] secret = KeyGeneration.GenerateRandomKey(20);
        string base32 = Base32Encoding.ToString(secret);
        user.SetTwoFactorPendingSecret(base32);
        await _uow.SaveChangesAsync(cancellationToken);

        const string issuer = "BaseProject";
        string uri =
            $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(user.Email)}?secret={base32}&issuer={Uri.EscapeDataString(issuer)}";

        return new TwoFactorSetupResponse(base32, uri);
    }
}

public sealed record ConfirmTwoFactorSetupCommand(Guid UserId, string Code) : IRequest<Result<bool, Error>>;

public sealed class ConfirmTwoFactorSetupCommandHandler : IRequestHandler<ConfirmTwoFactorSetupCommand, Result<bool, Error>>
{
    private readonly IUserWriteRepository _users;
    private readonly IUnitOfWork _uow;

    public ConfirmTwoFactorSetupCommandHandler(IUserWriteRepository users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(ConfirmTwoFactorSetupCommand request, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdForUpdateAsync(request.UserId, cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.TwoFactorSecret))
            return new InvalidCredentialsError("Two-factor setup was not started.");

        var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));
        if (!totp.VerifyTotp(request.Code, out _, new VerificationWindow(previous: 1, future: 1)))
            return new InvalidCredentialsError("Invalid authenticator code.");

        user.ActivateTwoFactor();
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}
