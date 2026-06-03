namespace Application.Common.Email;

/// <summary>Placeholder keys used in <c>Email/Templates/*.html</c> as <c>[[key]]</c>.</summary>
public static class EmailTemplateTokens
{
    public const string AppName = "appName";
    public const string GreetingName = "greetingName";
    public const string Headline = "headline";
    public const string ActionUrl = "actionUrl";
    public const string ActionLabel = "actionLabel";
    public const string FooterNote = "footerNote";
    public const string TtlMinutes = "ttlMinutes";
    public const string Year = "year";
    /// <summary>Optional extra HTML block (already safe or empty).</summary>
    public const string FallbackHtml = "fallbackHtml";

    /// <summary>Tokens inserted without HTML encoding (must be trusted URLs from the app).</summary>
    public static readonly HashSet<string> Raw = new(StringComparer.Ordinal)
    {
        ActionUrl,
        FallbackHtml
    };
}
