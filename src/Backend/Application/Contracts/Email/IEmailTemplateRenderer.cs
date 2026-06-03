namespace Application.Contracts.Email;

/// <summary>
///     Renders HTML email templates from disk. Templates use <c>[[tokenName]]</c> placeholders
///     (see <c>Application.Common.Email.EmailTemplateNames</c> and <c>EmailTemplateTokens</c>).
/// </summary>
public interface IEmailTemplateRenderer
{
    /// <param name="templateName">File name without extension, e.g. <see cref="EmailTemplateNames.VerifyEmail" />.</param>
    /// <param name="tokens">Token name → value. URL tokens listed in <paramref name="rawTokenNames"/> are not HTML-encoded.</param>
    /// <param name="rawTokenNames">Optional set of token names inserted verbatim (e.g. action links).</param>
    string Render(
        string templateName,
        IReadOnlyDictionary<string, string?> tokens,
        IReadOnlySet<string>? rawTokenNames = null);
}
