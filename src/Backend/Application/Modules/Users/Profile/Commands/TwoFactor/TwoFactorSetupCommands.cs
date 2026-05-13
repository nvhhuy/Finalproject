using Application.Contracts.Modules.Users.Persistence;
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
    private readonly IUserRespository _users;

    public BeginTwoFactorSetupCommandHandler(IUserRespository users) => _users = users;

    public async Task<Result<TwoFactorSetupResponse, Error>> Handle(BeginTwoFactorSetupCommand request, CancellationToken cancellationToken)
    {
        User? user = await _users.GetUserByIdForUpdateAsync(request.UserId, cancellationToken);
        if (user is null)
            return new InvalidCredentialsError("User was not found.");

        byte[] secret = KeyGeneration.GenerateRandomKey(20);
        string base32 = Base32Encoding.ToString(secret);
        user.SetTwoFactorPendingSecret(base32);
        await _users.SaveChangesAsync(cancellationToken);

        const string issuer = "FinalProject";
        string uri =
            $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(user.Email)}?secret={base32}&issuer={Uri.EscapeDataString(issuer)}";

        return new TwoFactorSetupResponse(base32, uri);
    }
}

public sealed record ConfirmTwoFactorSetupCommand(Guid UserId, string Code) : IRequest<Result<bool, Error>>;

public sealed class ConfirmTwoFactorSetupCommandHandler : IRequestHandler<ConfirmTwoFactorSetupCommand, Result<bool, Error>>
{
    private readonly IUserRespository _users;

    public ConfirmTwoFactorSetupCommandHandler(IUserRespository users) => _users = users;

    public async Task<Result<bool, Error>> Handle(ConfirmTwoFactorSetupCommand request, CancellationToken cancellationToken)
    {
        User? user = await _users.GetUserByIdForUpdateAsync(request.UserId, cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.TwoFactorSecret))
            return new InvalidCredentialsError("Two-factor setup was not started.");

        var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));
        if (!totp.VerifyTotp(request.Code, out _, new VerificationWindow(previous: 1, future: 1)))
            return new InvalidCredentialsError("Invalid authenticator code.");

        user.ActivateTwoFactor();
        await _users.SaveChangesAsync(cancellationToken);
        return true;
    }
}
