using System.Security.Cryptography;
using Application.Contracts.Caching;
using Application.Contracts.Email;
using Application.Modules.Users.Authentication;
using Application.Modules.Users.Authentication.Commands.VerifyEmail;
using Application.Modules.Users.Authentication.Options;
using Microsoft.Extensions.Options;
using Shared.Security;

namespace Infrastructure.Email;

public sealed class EmailVerificationSender : IEmailVerificationSender
{
    private readonly ICacheService _cache;
    private readonly IEmailSender _emailSender;
    private readonly PasswordResetEmailOptions _options;

    public EmailVerificationSender(
        ICacheService cache,
        IEmailSender emailSender,
        IOptions<PasswordResetEmailOptions> options)
    {
        _cache = cache;
        _emailSender = emailSender;
        _options = options.Value;
    }

    public async Task SendVerificationEmailAsync(Guid userId, string email,
        CancellationToken cancellationToken = default)
    {
        var tokenId = Guid.NewGuid().ToString("N");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = TokenHasher.Sha256(token);
        var cacheKey = AuthCacheKeys.EmailVerificationPrefix + tokenId;

        var ttlMinutes = _options.TokenTtlMinutes > 0 ? _options.TokenTtlMinutes : 30;
        await _cache.SetAsync(
            cacheKey,
            new EmailVerificationTokenPayload(userId, tokenHash),
            TimeSpan.FromMinutes(ttlMinutes),
            cancellationToken);

        var body = BuildBody(tokenId, token, ttlMinutes);
        await _emailSender.SendAsync(email, "Confirm your email address", body, cancellationToken);
    }

    private string BuildBody(string tokenId, string token, int ttlMinutes)
    {
        var baseUrl = (_options.PublicAppBaseUrl ?? "").Trim().TrimEnd('/');
        string instructions;
        if (!string.IsNullOrEmpty(baseUrl))
        {
            var query = $"tokenId={Uri.EscapeDataString(tokenId)}&token={Uri.EscapeDataString(token)}";
            instructions =
                $"Open this link to confirm your email (valid for {ttlMinutes} minutes):{Environment.NewLine}" +
                $"{baseUrl}/verify-email?{query}";
        }
        else
        {
            instructions =
                $"Use the verify-email screen with these values (valid for {ttlMinutes} minutes):{Environment.NewLine}" +
                $"Token ID: {tokenId}{Environment.NewLine}" +
                $"Token: {token}";
        }

        return
            "Thanks for registering. Please confirm your email address." +
            $"{Environment.NewLine}{Environment.NewLine}{instructions}" +
            $"{Environment.NewLine}{Environment.NewLine}If you did not create an account, you can ignore this message.";
    }
}