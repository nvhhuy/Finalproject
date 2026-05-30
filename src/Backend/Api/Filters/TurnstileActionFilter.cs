using Api.Models;
using Application.Contracts.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Filters;

/// <summary>
///     Global action filter that enforces Cloudflare Turnstile verification on actions decorated
///     with <see cref="RequiresTurnstileAttribute" />.
/// </summary>
public sealed class TurnstileActionFilter : IAsyncActionFilter, IOrderedFilter
{
    public const string TokenHeader = "X-Turnstile-Token";

    private readonly ITurnstileService _turnstile;

    public TurnstileActionFilter(ITurnstileService turnstile)
    {
        _turnstile = turnstile;
    }

    /// <summary>Run before <see cref="IdempotencyActionFilter" /> (order -2000) but after auth.</summary>
    public int Order => -1900;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var hasAttribute = context.ActionDescriptor.EndpointMetadata
            .OfType<RequiresTurnstileAttribute>()
            .Any();

        if (!hasAttribute)
        {
            await next();
            return;
        }

        var token = context.HttpContext.Request.Headers[TokenHeader].FirstOrDefault();
        var remoteIp = context.HttpContext.Connection.RemoteIpAddress?.ToString();

        var valid = await _turnstile.ValidateAsync(token, remoteIp, context.HttpContext.RequestAborted);
        if (!valid)
        {
            context.Result = new ObjectResult(
                ApiResponse<object?>.Fail(
                    new ApiErrorPayload(
                        "TurnstileFailed",
                        "Human verification failed. Please refresh the page and try again.",
                        StatusCodes.Status400BadRequest),
                    context.HttpContext))
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
            return;
        }

        await next();
    }
}
