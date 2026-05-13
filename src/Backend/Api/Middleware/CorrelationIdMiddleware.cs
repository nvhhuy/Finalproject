namespace Api.Middleware;

/// <summary>
/// Ensures every response has a correlation id (for logs, support, distributed tracing).
/// Accepts incoming <c>X-Correlation-Id</c> or generates one; exposes it on <see cref="HttpContext.Items"/> as <c>CorrelationId</c>.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string HttpContextItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        string id = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");
        context.Items[HttpContextItemKey] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Append(HeaderName, id);
            return Task.CompletedTask;
        });
        await _next(context);
    }
}
