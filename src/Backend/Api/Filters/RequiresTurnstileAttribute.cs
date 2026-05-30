namespace Api.Filters;

/// <summary>
///     Marks an action as requiring a valid Cloudflare Turnstile token.
///     The token must be supplied by the client in the <c>X-Turnstile-Token</c> request header.
///     Validation is performed by <see cref="TurnstileActionFilter" />.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequiresTurnstileAttribute : Attribute;
