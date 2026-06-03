using Application.Common.Email;
using Application.Contracts.Email;
using Application.Modules.Users.Authentication.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

public sealed class WelcomeEmailSender : IWelcomeEmailSender
{
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateRenderer _templates;
    private readonly PasswordResetEmailOptions _options;
    private readonly EmailBrandingOptions _branding;

    public WelcomeEmailSender(
        IEmailSender emailSender,
        IEmailTemplateRenderer templates,
        IOptions<PasswordResetEmailOptions> options,
        IOptions<EmailBrandingOptions> branding)
    {
        _emailSender = emailSender;
        _templates = templates;
        _options = options.Value;
        _branding = branding.Value;
    }

    public Task SendWelcomeEmailAsync(string email, string? displayName,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = (_options.PublicAppBaseUrl ?? "").Trim().TrimEnd('/');
        var loginUrl = string.IsNullOrEmpty(baseUrl) ? "/" : $"{baseUrl}/login";
        var appName = string.IsNullOrWhiteSpace(_branding.AppName) ? "App" : _branding.AppName.Trim();
        var tokens = EmailTemplateTokenBuilder.ForWelcome(appName, loginUrl, displayName);
        var html = _templates.Render(EmailTemplateNames.Welcome, tokens);
        return _emailSender.SendAsync(email, $"Welcome to {appName}", html, cancellationToken);
    }
}
