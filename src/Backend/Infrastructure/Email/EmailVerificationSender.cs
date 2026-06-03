using System.Security.Cryptography;
using Application.Common.Email;
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
    private readonly IEmailTemplateRenderer _templates;
    private readonly PasswordResetEmailOptions _options;
    private readonly EmailBrandingOptions _branding;

    public EmailVerificationSender(
        ICacheService cache,
        IEmailSender emailSender,
        IEmailTemplateRenderer templates,
        IOptions<PasswordResetEmailOptions> options,
        IOptions<EmailBrandingOptions> branding)
    {
        _cache = cache;
        _emailSender = emailSender;
        _templates = templates;
        _options = options.Value;
        _branding = branding.Value;
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
        var appName = string.IsNullOrWhiteSpace(_branding.AppName) ? "App" : _branding.AppName.Trim();
        var baseUrl = (_options.PublicAppBaseUrl ?? "").Trim().TrimEnd('/');
        string actionUrl;
        string? fallbackHtml = null;

        if (!string.IsNullOrEmpty(baseUrl))
        {
            var query = $"tokenId={Uri.EscapeDataString(tokenId)}&token={Uri.EscapeDataString(token)}";
            actionUrl = $"{baseUrl}/verify-email?{query}";
        }
        else
        {
            actionUrl = "#";
            var fallback =
                $"Use the verify-email screen (valid for {ttlMinutes} minutes). Token ID: {tokenId} Token: {token}";
            fallbackHtml = EmailTemplateTokenBuilder.FallbackParagraph(fallback);
        }

        var tokens = EmailTemplateTokenBuilder.ForVerifyEmail(appName, actionUrl, ttlMinutes, fallbackHtml: fallbackHtml);
        return _templates.Render(EmailTemplateNames.VerifyEmail, tokens);
    }
}
