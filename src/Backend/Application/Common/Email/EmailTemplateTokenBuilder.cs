namespace Application.Common.Email;

/// <summary>Helpers to build token dictionaries for <see cref="Contracts.Email.IEmailTemplateRenderer" />.</summary>
public static class EmailTemplateTokenBuilder
{
    public static Dictionary<string, string?> ForVerifyEmail(
        string appName,
        string actionUrl,
        int ttlMinutes,
        string? recipientName = null,
        string? fallbackHtml = null) =>
        BaseAuthEmail(appName, "Confirm your email", "Confirm email",
            "If you did not create an account, you can ignore this message.",
            actionUrl, ttlMinutes, recipientName, fallbackHtml);

    public static Dictionary<string, string?> ForPasswordReset(
        string appName,
        string actionUrl,
        int ttlMinutes,
        string? recipientName = null,
        string? fallbackHtml = null) =>
        BaseAuthEmail(appName, "Reset your password", "Reset password",
            "If you did not request a reset, you can ignore this message.",
            actionUrl, ttlMinutes, recipientName, fallbackHtml);

    public static Dictionary<string, string?> ForWelcome(
        string appName,
        string loginUrl,
        string? recipientName = null) =>
        new()
        {
            [EmailTemplateTokens.AppName] = appName,
            [EmailTemplateTokens.GreetingName] = GreetingName(recipientName),
            [EmailTemplateTokens.Headline] = "Welcome",
            [EmailTemplateTokens.ActionUrl] = loginUrl,
            [EmailTemplateTokens.ActionLabel] = "Open app",
            [EmailTemplateTokens.FooterNote] =
                "Need help? Reply to this email or contact support from the app.",
            [EmailTemplateTokens.Year] = DateTime.UtcNow.Year.ToString(),
            [EmailTemplateTokens.TtlMinutes] = "",
            [EmailTemplateTokens.FallbackHtml] = ""
        };

    private static Dictionary<string, string?> BaseAuthEmail(
        string appName,
        string headline,
        string actionLabel,
        string footerNote,
        string actionUrl,
        int ttlMinutes,
        string? recipientName,
        string? fallbackHtml) =>
        new()
        {
            [EmailTemplateTokens.AppName] = appName,
            [EmailTemplateTokens.GreetingName] = GreetingName(recipientName),
            [EmailTemplateTokens.Headline] = headline,
            [EmailTemplateTokens.ActionUrl] = actionUrl,
            [EmailTemplateTokens.ActionLabel] = actionLabel,
            [EmailTemplateTokens.FooterNote] = footerNote,
            [EmailTemplateTokens.TtlMinutes] = ttlMinutes.ToString(),
            [EmailTemplateTokens.Year] = DateTime.UtcNow.Year.ToString(),
            [EmailTemplateTokens.FallbackHtml] = fallbackHtml ?? ""
        };

    public static string GreetingName(string? recipientName) =>
        string.IsNullOrWhiteSpace(recipientName) ? "there" : recipientName.Trim();

    public static string FallbackParagraph(string text) =>
        $"<p style=\"color:#6b7280;font-size:13px;\">{System.Net.WebUtility.HtmlEncode(text)}</p>";
}
