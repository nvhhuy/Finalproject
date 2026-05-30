namespace Api.Configuration;

/// <summary>
///     CORS policy configuration. Bound from <c>Cors</c> section.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why explicit origins:</b> the API issues credentialed requests (cookies for refresh-
///         token rotation; bearer tokens), so <c>AllowAnyOrigin()</c> is forbidden by the browser. We
///         must enumerate allowed origins exactly — wildcard subdomains aren't supported by the spec
///         when credentials are involved.
///     </para>
///     <para>
///         <b>Headers:</b> <see cref="ExposedHeaders" /> defaults to <c>X-Correlation-Id</c> so a
///         browser-side error reporter can correlate to server logs. Add any custom response headers
///         the SPA needs to read here.
///     </para>
/// </remarks>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "frontend";

    /// <summary>
    ///     Origins the SPA(s) are served from (e.g. <c>http://localhost:5173</c>, <c>https://app.example.com</c>).
    ///     Each entry must be an absolute origin (scheme + host + optional port). Empty array
    ///     disables CORS for browser clients — fine for backend-to-backend services.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    /// <summary>Response headers the browser is allowed to read from the JS layer.</summary>
    public string[] ExposedHeaders { get; set; } = { "X-Correlation-Id" };

    /// <summary>
    ///     Request headers the browser is allowed to send on credentialed CORS. Pinned explicitly
    ///     (instead of <c>AllowAnyHeader</c>) so a future header-injection bug in the SPA can't
    ///     smuggle arbitrary headers across origins.
    /// </summary>
    public string[] AllowedHeaders { get; set; } =
    {
        "Content-Type",
        "Authorization",
        "X-Correlation-Id",
        "x-api-version",
        "Accept",
        "Accept-Language",
        "Idempotency-Key",
        "X-Turnstile-Token"
    };

    /// <summary>HTTP methods the SPA is allowed to use cross-origin.</summary>
    public string[] AllowedMethods { get; set; } =
    {
        "GET",
        "POST",
        "PUT",
        "PATCH",
        "DELETE",
        "OPTIONS"
    };

    /// <summary>How long the browser is allowed to cache the CORS preflight response (seconds).</summary>
    public int PreflightMaxAgeSeconds { get; set; } = 600;

    /// <summary>
    ///     Whether to send cookies / Authorization header with cross-origin requests. Required for
    ///     JWT bearer flows when the token lives in a cookie; harmless when it's localStorage.
    /// </summary>
    public bool AllowCredentials { get; set; } = true;
}