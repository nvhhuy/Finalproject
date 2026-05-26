using Application.Contracts.Services;

namespace Api.Middleware;

/// <summary>
/// Ensures every response has a correlation id (for logs, support, distributed tracing).
/// Accepts incoming <c>X-Correlation-Id</c> or generates one; exposes it on:
/// <list type="bullet">
///   <item><description><see cref="HttpContext.Items"/>[<c>CorrelationId</c>] — for handlers that prefer HTTP context.</description></item>
///   <item><description><see cref="ICorrelationContext"/>.Current — for Application/Infrastructure code that can't depend on ASP.NET (outbox writer, queue publisher).</description></item>
/// </list>
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string HttpContextItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlationContext)
    {
        string id = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");
        context.Items[HttpContextItemKey] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Append(HeaderName, id);
            return Task.CompletedTask;
        });

        // BeginScope returns a disposable that restores the previous value on dispose. Critical
        // for nested requests (e.g. an internal HTTP callback inside the same process) — without
        // the scope restore, the inner request's correlation id would clobber the outer.
        using (correlationContext.BeginScope(id))
        {
            await _next(context);
        }
    }
}
