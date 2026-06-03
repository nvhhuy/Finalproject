using System.Net;
using System.Security.Cryptography;
using Application.Contracts.Caching;
using Application.Contracts.Email;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Transactions;
using Application.Common.Email;
using Application.Contracts.Email;
using Application.Modules.Users.Authentication.Options;
using Domains.UserAggregate;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Shared.Security;

namespace Application.Modules.Users.Authentication.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest<Result<bool, Error>>;

public sealed record ResetPasswordCommand(string TokenId, string Token, string NewPassword)
    : IRequest<Result<bool, Error>>;

internal sealed record PasswordResetTokenPayload(Guid UserId, string TokenHash);

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result<bool, Error>>
{
    private readonly ICacheService _cache;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateRenderer _templates;
    private readonly PasswordResetEmailOptions _resetEmail;
    private readonly EmailBrandingOptions _branding;
    private readonly IUserReadRepository _users;

    public ForgotPasswordCommandHandler(
        IUserReadRepository users,
        ICacheService cache,
        IEmailSender emailSender,
        IEmailTemplateRenderer templates,
        IOptions<PasswordResetEmailOptions> resetEmailOptions,
        IOptions<EmailBrandingOptions> brandingOptions)
    {
        _users = users;
        _cache = cache;
        _emailSender = emailSender;
        _templates = templates;
        _resetEmail = resetEmailOptions.Value;
        _branding = brandingOptions.Value;
    }

    public async Task<Result<bool, Error>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByEmailAsync(request.Email, cancellationToken: cancellationToken);
        if (user is null)
            return true; // prevent account enumeration

        var tokenId = Guid.NewGuid().ToString("N");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = TokenHasher.Sha256(token);
        var cacheKey = AuthCacheKeys.PasswordResetPrefix + tokenId;

        var ttlMinutes = _resetEmail.TokenTtlMinutes > 0 ? _resetEmail.TokenTtlMinutes : 30;
        var ttl = TimeSpan.FromMinutes(ttlMinutes);

        await _cache.SetAsync(cacheKey, new PasswordResetTokenPayload(user.Id.Value, tokenHash), ttl,
            cancellationToken);

        var appName = string.IsNullOrWhiteSpace(_branding.AppName) ? "App" : _branding.AppName.Trim();
        var baseUrl = (_resetEmail.PublicAppBaseUrl ?? "").Trim().TrimEnd('/');
        string actionUrl;
        string? fallbackHtml = null;

        if (!string.IsNullOrEmpty(baseUrl))
        {
            var query = $"tokenId={Uri.EscapeDataString(tokenId)}&token={Uri.EscapeDataString(token)}";
            actionUrl = $"{baseUrl}/reset-password?{query}";
        }
        else
        {
            actionUrl = "#";
            var fallback =
                $"Use the reset screen. Token ID: {tokenId} Token: {token} (valid {ttlMinutes} min)";
            fallbackHtml = EmailTemplateTokenBuilder.FallbackParagraph(fallback);
        }

        var tokens = EmailTemplateTokenBuilder.ForPasswordReset(
            appName, actionUrl, ttlMinutes, user.FirstName, fallbackHtml);
        var body = _templates.Render(EmailTemplateNames.PasswordReset, tokens);
        await _emailSender.SendAsync(user.Email, "Password reset request", body, cancellationToken);
        return true;
    }
}

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result<bool, Error>>
{
    private readonly ICacheService _cache;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IUnitOfWork _uow;
    private readonly IUserWriteRepository _users;

    public ResetPasswordCommandHandler(
        IUserWriteRepository users,
        ICacheService cache,
        IPasswordHasher<User> passwordHasher,
        IUnitOfWork uow)
    {
        _users = users;
        _cache = cache;
        _passwordHasher = passwordHasher;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var cacheKey = AuthCacheKeys.PasswordResetPrefix + request.TokenId;
        var payload = await _cache.GetAsync<PasswordResetTokenPayload>(cacheKey, cancellationToken);
        if (payload is null)
            return new Error("InvalidOrExpiredResetToken", (int)HttpStatusCode.BadRequest,
                "Reset token is invalid or expired.");

        var hash = TokenHasher.Sha256(request.Token);
        if (!string.Equals(payload.TokenHash, hash, StringComparison.Ordinal))
            return new Error("InvalidOrExpiredResetToken", (int)HttpStatusCode.BadRequest,
                "Reset token is invalid or expired.");

        var user = await _users.GetByIdForUpdateAsync(payload.UserId, cancellationToken);
        if (user is null)
            return new Error("InvalidResetTarget", (int)HttpStatusCode.BadRequest,
                "Reset token target is no longer available.");

        user.UpdatePasswordHash(_passwordHasher.HashPassword(user, request.NewPassword));
        await _uow.SaveChangesAsync(cancellationToken);
        await _users.InvalidateUserCacheAsync(user.Id.Value, user.Email, user.UserName, cancellationToken);
        await _cache.RemoveAsync(cacheKey, cancellationToken);
        return true;
    }
}