using Api.Middleware;
using Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Api.Authentication;

/// <summary>
///     Replaces the default JWT bearer challenge / forbid responses with the standard
///     <see cref="ApiResponse{T}" /> envelope. The default <c>ProblemDetails</c> shape used by
///     <c>AddJwtBearer</c> breaks client SDKs that assume <c>{ success, data, error, meta }</c> on
///     every endpoint — they crash on 401/403 because <c>response.error</c> doesn't exist.
/// </summary>
/// <remarks>
///     <para>
///         This is wired from <c>Program.cs</c> via <see cref="IServiceCollection" /> after
///         <c>AddInfrastructure</c> has registered the bearer scheme. The Infrastructure layer would
///         have a hard time emitting this envelope itself because the envelope type lives in the Api
///         project — and we don't want Infrastructure to know about HTTP-shape concerns.
///     </para>
///     <para>
///         <b>Other handlers preserved</b>: <c>OnMessageReceived</c> set by Infrastructure (SignalR
///         query-string token pickup) is intentionally not overridden — we copy the existing handler
///         reference before clobbering <c>OnChallenge</c> / <c>OnForbidden</c>.
///     </para>
/// </remarks>
public static class JwtEnvelopeEvents
{
    public static IServiceCollection AddJwtEnvelopeErrorResponses(this IServiceCollection services)
    {
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .PostConfigure(options =>
            {
                var existing = options.Events ?? new JwtBearerEvents();
                var originalMessageReceived = existing.OnMessageReceived;

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = originalMessageReceived ?? (_ => Task.CompletedTask),
                    OnChallenge = context => WriteFailureAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                        "Unauthorized", "Authentication is required to access this resource.",
                        () => context.HandleResponse()),
                    OnForbidden = context => WriteFailureAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                        "Forbidden", "Your account does not have permission to access this resource.", null)
                };
            });

        return services;
    }

    private static async Task WriteFailureAsync(HttpContext httpContext, int status, string code, string message,
        Action? afterHandle)
    {
        afterHandle?.Invoke();

        if (httpContext.Response.HasStarted)
            return; // Another middleware already wrote the response — don't double-emit.

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/json";

        var envelope = ApiResponse<object?>.Fail(
            new ApiErrorPayload(code, message, status),
            httpContext);

        // Echo the correlation id back as a response header even on auth failures so client
        // support tickets can match the offending request to server logs.
        if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.HttpContextItemKey, out var cid) &&
            cid is string correlationId)
            httpContext.Response.Headers.TryAdd(CorrelationIdMiddleware.HeaderName, correlationId);

        await httpContext.Response.WriteAsJsonAsync(envelope);
    }
}