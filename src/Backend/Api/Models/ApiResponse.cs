namespace Api.Models;

/// <summary>
/// Standard API envelope: same top-level shape for success and failure (easy for clients and code generators to extend).
/// Matches the spirit of Microsoft guidance (explicit contracts at the API boundary; domain stays in Application/Domains).
/// </summary>
public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    ApiErrorPayload? Error,
    ApiMeta Meta)
{
    public static ApiResponse<T> Ok(T data, HttpContext httpContext) =>
        new(true, data, null, ApiMeta.FromHttpContext(httpContext));

    public static ApiResponse<object?> OkEmpty(HttpContext httpContext) =>
        new(true, null, null, ApiMeta.FromHttpContext(httpContext));

    public static ApiResponse<T?> Fail(ApiErrorPayload error, HttpContext httpContext) =>
        new(false, default, error, ApiMeta.FromHttpContext(httpContext));
}
