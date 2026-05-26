using Application.Contracts.Caching;
using Application.Contracts.Email;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Transactions;
using Application.Modules.Users.Authentication.Options;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Shared.Common;
using Shared.Errors;
using Shared.Security;
using System.Security.Cryptography;

namespace Application.Modules.Users.Authentication.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest<Result<bool, Error>>;
public sealed record ResetPasswordCommand(string TokenId, string Token, string NewPassword) : IRequest<Result<bool, Error>>;

internal sealed record PasswordResetTokenPayload(Guid UserId, string TokenHash);

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result<bool, Error>>
{
    private readonly IUserReadRepository _users;
    private readonly ICacheService _cache;
    private readonly IEmailSender _emailSender;
    private readonly PasswordResetEmailOptions _resetEmail;

    public ForgotPasswordCommandHandler(
        IUserReadRepository users,
        ICacheService cache,
        IEmailSender emailSender,
        IOptions<PasswordResetEmailOptions> resetEmailOptions)
    {
        _users = users;
        _cache = cache;
        _emailSender = emailSender;
        _resetEmail = resetEmailOptions.Value;
    }

    public async Task<Result<bool, Error>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        Result<bool, ValidationError> validation = await request.ValidateAsync(new ForgotPasswordCommandValidator(), cancellationToken);
        if (validation.IsFailure)
            return validation.Error;

        Domains.UserAggregate.User? user = await _users.GetByEmailAsync(request.Email, cancellationToken: cancellationToken);
        if (user is null)
            return true; // prevent account enumeration

        string tokenId = Guid.NewGuid().ToString("N");
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string tokenHash = TokenHasher.Sha256(token);
        string cacheKey = AuthCacheKeys.PasswordResetPrefix + tokenId;

        int ttlMinutes = _resetEmail.TokenTtlMinutes > 0 ? _resetEmail.TokenTtlMinutes : 30;
        TimeSpan ttl = TimeSpan.FromMinutes(ttlMinutes);

        await _cache.SetAsync(cacheKey, new PasswordResetTokenPayload(user.Id.Value, tokenHash), ttl, cancellationToken);

        string baseUrl = (_resetEmail.PublicAppBaseUrl ?? "").Trim().TrimEnd('/');
        string instructions;
        if (!string.IsNullOrEmpty(baseUrl))
        {
            string query = $"tokenId={Uri.EscapeDataString(tokenId)}&token={Uri.EscapeDataString(token)}";
            instructions =
                $"Open this link to choose a new password (valid for {ttlMinutes} minutes):{Environment.NewLine}" +
                $"{baseUrl}/reset-password?{query}";
        }
        else
        {
            instructions =
                $"Use the password reset screen in the app with these values (valid for {ttlMinutes} minutes):{Environment.NewLine}" +
                $"Token ID: {tokenId}{Environment.NewLine}" +
                $"Token: {token}";
        }

        string body =
            "We received a request to reset your password." +
            $"{Environment.NewLine}{Environment.NewLine}{instructions}" +
            $"{Environment.NewLine}{Environment.NewLine}If you did not request this, you can ignore this message.";
        await _emailSender.SendAsync(user.Email, "Password reset request", body, cancellationToken);
        return true;
    }
}

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result<bool, Error>>
{
    private readonly IUserWriteRepository _users;
    private readonly ICacheService _cache;
    private readonly IPasswordHasher<Domains.UserAggregate.User> _passwordHasher;
    private readonly IUnitOfWork _uow;

    public ResetPasswordCommandHandler(
        IUserWriteRepository users,
        ICacheService cache,
        IPasswordHasher<Domains.UserAggregate.User> passwordHasher,
        IUnitOfWork uow)
    {
        _users = users;
        _cache = cache;
        _passwordHasher = passwordHasher;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        Result<bool, ValidationError> validation = await request.ValidateAsync(new ResetPasswordCommandValidator(), cancellationToken);
        if (validation.IsFailure)
            return validation.Error;

        string cacheKey = AuthCacheKeys.PasswordResetPrefix + request.TokenId;
        PasswordResetTokenPayload? payload = await _cache.GetAsync<PasswordResetTokenPayload>(cacheKey, cancellationToken);
        if (payload is null)
            return new Error("InvalidOrExpiredResetToken", StatusCodes.Status400BadRequest, "Reset token is invalid or expired.");

        string hash = TokenHasher.Sha256(request.Token);
        if (!string.Equals(payload.TokenHash, hash, StringComparison.Ordinal))
            return new Error("InvalidOrExpiredResetToken", StatusCodes.Status400BadRequest, "Reset token is invalid or expired.");

        Domains.UserAggregate.User? user = await _users.GetByIdForUpdateAsync(payload.UserId, cancellationToken);
        if (user is null)
            return new Error("InvalidResetTarget", StatusCodes.Status400BadRequest, "Reset token target is no longer available.");

        user.UpdatePasswordHash(_passwordHasher.HashPassword(user, request.NewPassword));
        await _uow.SaveChangesAsync(cancellationToken);
        await _users.InvalidateUserCacheAsync(user.Id.Value, user.Email, user.UserName, cancellationToken);
        await _cache.RemoveAsync(cacheKey, cancellationToken);
        return true;
    }
}
