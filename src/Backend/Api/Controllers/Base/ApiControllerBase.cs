using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;
using System.Security.Claims;

namespace Api.Controllers.Base;

/// <summary>Shared HTTP concerns: MediatR sender, user id resolution, <see cref="ApiResponse{T}"/> mapping.</summary>
public abstract class ApiControllerBase : ControllerBase
{
    protected readonly ISender Sender;

    protected ApiControllerBase(ISender sender) => Sender = sender;

    protected Guid CurrentUserId =>
        TryGetUserId(out Guid id)
            ? id
            : throw new InvalidOperationException("Authenticated user id claim is missing.");

    protected Guid CurrentAdminId => CurrentUserId;

    protected bool TryGetUserId(out Guid userId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (!string.IsNullOrEmpty(sub) && Guid.TryParse(sub, out userId))
            return true;
        userId = default;
        return false;
    }

    protected IActionResult OkApi<T>(T data) =>
        Ok(ApiResponse<T>.Ok(data, HttpContext));

    protected IActionResult OkApiEmpty() =>
        Ok(ApiResponse<object?>.OkEmpty(HttpContext));

    protected IActionResult FailApi(Error error, int? overrideStatus = null)
    {
        int status = overrideStatus ?? error.StatusCode ?? StatusCodes.Status400BadRequest;
        ApiErrorPayload payload = ToErrorPayload(error);
        return StatusCode(status, ApiResponse<object?>.Fail(payload, HttpContext));
    }

    protected IActionResult BadRequestValidationApi(ValidationError validation) =>
        BadRequest(ApiResponse<object?>.Fail(ToErrorPayload(validation), HttpContext));

    /// <summary>Maps <see cref="Result{T,E}"/> to HTTP using the standard envelope (except special auth flows handled separately).</summary>
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
