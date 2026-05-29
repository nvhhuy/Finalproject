namespace Api.Models;

/// <summary>
///     Standard API envelope: same top-level shape for success and failure (easy for clients and code generators to
///     extend).
///     Matches the spirit of Microsoft guidance (explicit contracts at the API boundary; domain stays in
///     Application/Domains).
/// </summary>
public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    ApiErrorPayload? Error,
    ApiMeta Meta)
{
    public static ApiResponse<T> Ok(T data, HttpContext httpContext)
    {
        return new ApiResponse<T>(true, data, null, ApiMeta.FromHttpContext(httpContext));
    }

    public static ApiResponse<object?> OkEmpty(HttpContext httpContext)
    {
        return new ApiResponse<object?>(true, null, null, ApiMeta.FromHttpContext(httpContext));
    }

    public static ApiResponse<T?> Fail(ApiErrorPayload error, HttpContext httpContext)
    {
        return new ApiResponse<T?>(false, default, error, ApiMeta.FromHttpContext(httpContext));
    }
}