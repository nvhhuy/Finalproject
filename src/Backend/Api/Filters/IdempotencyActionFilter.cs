using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Api.Models;
using Application.Contracts.Idempotency;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Shared.Errors;

namespace Api.Filters;

/// <summary>
///     Enforces idempotency keys on actions marked with <see cref="RequiresIdempotencyKeyAttribute" />.
/// </summary>
public sealed class IdempotencyActionFilter : IAsyncActionFilter, IOrderedFilter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IIdempotencyService _idempotency;

    public IdempotencyActionFilter(IIdempotencyService idempotency)
    {
        _idempotency = idempotency;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var attribute = context.ActionDescriptor.EndpointMetadata
            .OfType<RequiresIdempotencyKeyAttribute>()
            .FirstOrDefault();

        if (attribute is null)
        {
            await next();
            return;
        }

        var key = context.HttpContext.Request.Headers[RequiresIdempotencyKeyAttribute.HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            context.Result = new BadRequestObjectResult(
                ApiResponse<object?>.Fail(
                    new ApiErrorPayload(
                        "IdempotencyKeyRequired",
                        $"Header '{RequiresIdempotencyKeyAttribute.HeaderName}' is required for this endpoint.",
                        StatusCodes.Status400BadRequest),
                    context.HttpContext));
            return;
        }

        var scope = attribute.Scope ?? BuildScope(context);
        var ownerPartition = ResolveOwnerPartition(context.HttpContext.User);

        var acquire = await _idempotency.TryAcquireAsync(
            scope,
            ownerPartition,
            key,
            context.HttpContext.RequestAborted);

        if (acquire.Status == IdempotencyAcquireStatus.Replay)
        {
            context.HttpContext.Response.Headers[RequiresIdempotencyKeyAttribute.ReplayHeaderName] = "true";
            context.Result = new ContentResult
            {
                StatusCode = acquire.ResponseStatusCode ?? StatusCodes.Status200OK,
                ContentType = "application/json",
                Content = acquire.ResponseBody
            };
            return;
        }

        if (acquire.Status == IdempotencyAcquireStatus.InProgress)
        {
            Error conflict = new("IdempotencyInProgress", StatusCodes.Status409Conflict,
                "A request with this idempotency key is already being processed.");
            context.Result = new ObjectResult(ApiResponse<object?>.Fail(
                new ApiErrorPayload(conflict.ErrorType, conflict.ErrorMessage, conflict.StatusCode),
                context.HttpContext))
            {
                StatusCode = StatusCodes.Status409Conflict
            };
            return;
        }

        var executed = await next();

        if (acquire.RecordId is null)
            return;

        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            await _idempotency.ReleaseAsync(acquire.RecordId.Value, context.HttpContext.RequestAborted);
            return;
        }

        var statusCode = GetStatusCode(executed.Result);
        if (statusCode is >= 200 and < 300)
        {
            var body = TrySerializeResult(executed.Result);
            if (body is not null)
                await _idempotency.CompleteAsync(acquire.RecordId.Value, statusCode, body,
                    context.HttpContext.RequestAborted);
            else
                await _idempotency.ReleaseAsync(acquire.RecordId.Value, context.HttpContext.RequestAborted);
        }
        else
        {
            await _idempotency.ReleaseAsync(acquire.RecordId.Value, context.HttpContext.RequestAborted);
        }
    }

    public int Order => -2000;

    private static string BuildScope(ActionExecutingContext context)
    {
        var method = context.HttpContext.Request.Method;
        var template = (context.ActionDescriptor as ControllerActionDescriptor)?.AttributeRouteInfo?.Template;
        return $"{method}:{template ?? context.HttpContext.Request.Path.Value}";
    }

    private static string ResolveOwnerPartition(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return string.IsNullOrEmpty(sub) ? "anonymous" : sub;
    }

    private static int GetStatusCode(IActionResult? result)
    {
        return result switch
        {
            IStatusCodeActionResult s when s.StatusCode.HasValue => s.StatusCode.Value,
            ObjectResult { StatusCode: int sc } => sc,
            _ => StatusCodes.Status200OK
        };
    }

    private static string? TrySerializeResult(IActionResult? result)
    {
        return result switch
        {
            ObjectResult { Value: not null } o => JsonSerializer.Serialize(o.Value, SerializerOptions),
            JsonResult { Value: not null } j => JsonSerializer.Serialize(j.Value, SerializerOptions),
            _ => null
        };
    }
}