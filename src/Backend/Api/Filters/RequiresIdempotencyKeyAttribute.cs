namespace Api.Filters;

/// <summary>
/// Requires the <c>Idempotency-Key</c> request header and deduplicates successful responses
/// for the configured <see cref="Scope"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequiresIdempotencyKeyAttribute : Attribute
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayHeaderName = "Idempotent-Replay";

    /// <summary>
    /// Logical operation name used to partition keys (e.g. <c>auth.register</c>).
    /// When empty, the filter derives scope from HTTP method + route template.
    /// </summary>
    public string? Scope { get; init; }
}
