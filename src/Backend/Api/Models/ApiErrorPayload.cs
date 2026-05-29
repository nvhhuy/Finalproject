namespace Api.Models;

/// <summary>
///     Stable error shape aligned with Problem Details fields (type/title/detail/status) plus validation and
///     extensions.
/// </summary>
public sealed record ApiErrorPayload(
    string Code,
    string? Message,
    int? HttpStatus,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    IReadOnlyDictionary<string, object>? Extensions = null);