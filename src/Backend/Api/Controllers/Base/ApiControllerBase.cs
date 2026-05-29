using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Api.Controllers.Base;

/// <summary>Shared HTTP concerns: MediatR sender, user id resolution, <see cref="ApiResponse{T}" /> mapping.</summary>
public abstract class ApiControllerBase : ControllerBase
{
    protected readonly ISender Sender;

    protected ApiControllerBase(ISender sender)
    {
        Sender = sender;
    }

    protected Guid CurrentUserId =>
        TryGetUserId(out var id)
            ? id
            : throw new InvalidOperationException("Authenticated user id claim is missing.");

    protected Guid CurrentAdminId => CurrentUserId;

    protected bool IsImpersonating =>
        User.FindFirst("impersonator_sub") is not null;

    protected Guid? CurrentImpersonatorId
    {
        get
        {
            var impersonator = User.FindFirstValue("impersonator_sub");
            return Guid.TryParse(impersonator, out var id) ? id : null;
        }
    }

    protected bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!string.IsNullOrEmpty(sub) && Guid.TryParse(sub, out userId))
            return true;
        userId = default;
        return false;
    }

    protected IActionResult OkApi<T>(T data)
    {
        return Ok(ApiResponse<T>.Ok(data, HttpContext));
    }

    protected IActionResult OkApiEmpty()
    {
        return Ok(ApiResponse<object?>.OkEmpty(HttpContext));
    }

    protected IActionResult FailApi(Error error, int? overrideStatus = null)
    {
        var status = overrideStatus ?? error.StatusCode ?? StatusCodes.Status400BadRequest;
        var payload = ToErrorPayload(error);
        return StatusCode(status, ApiResponse<object?>.Fail(payload, HttpContext));
    }

    protected IActionResult BadRequestValidationApi(ValidationError validation)
    {
        return BadRequest(ApiResponse<object?>.Fail(ToErrorPayload(validation), HttpContext));
    }

    /// <summary>
    ///     Maps <see cref="Result{T,E}" /> to HTTP using the standard envelope (except special auth flows handled
    ///     separately).
    /// </summary>
    protected IActionResult MatchApi<T>(Result<T, Error> result, Func<T, IActionResult>? success = null)
    {
        if (result.IsFailure)
        {
            if (result.Error is ValidationError ve)
                return BadRequestValidationApi(ve);
            return FailApi(result.Error);
        }

        return success is not null ? success(result.Value) : OkApi(result.Value);
    }

    protected IActionResult MatchApiEmpty(Result<bool, Error> result)
    {
        if (result.IsFailure)
        {
            if (result.Error is ValidationError ve)
                return BadRequestValidationApi(ve);
            return FailApi(result.Error);
        }

        return OkApiEmpty();
    }

    private static ApiErrorPayload ToErrorPayload(Error error)
    {
        if (error is ValidationError ve)
        {
            IReadOnlyDictionary<string, string[]> dict = ve.ValidationErrorDictionary
                .ToDictionary(kv => kv.Key, kv => new[] { kv.Value }, StringComparer.Ordinal);
            return new ApiErrorPayload(
                ve.ErrorType,
                ve.ErrorMessage,
                ve.StatusCode,
                dict);
        }

        return new ApiErrorPayload(error.ErrorType, error.ErrorMessage, error.StatusCode);
    }
}