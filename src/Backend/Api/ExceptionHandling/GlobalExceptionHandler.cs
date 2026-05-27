using Api.Models;
using Application.Contracts.Transactions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Shared.Errors.General;

namespace Api.ExceptionHandling;

/// <summary>Maps unhandled exceptions to RFC 7807 <see cref="ProblemDetails"/> without leaking stack traces outside Development.</summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IHostEnvironment environment, ILogger<GlobalExceptionHandler> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ConcurrencyConflictException)
        {
            ConcurrencyConflictError error = new();
            httpContext.Response.StatusCode = error.StatusCode ?? StatusCodes.Status409Conflict;
            await httpContext.Response.WriteAsJsonAsync(
                ApiResponse<object?>.Fail(
                    new ApiErrorPayload(error.ErrorType, error.ErrorMessage, error.StatusCode),
                    httpContext),
                cancellationToken);
            return true;
        }

        _logger.LogError(exception, "Unhandled exception");
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Type = "https://httpstatuses.io/500",
            Detail = _environment.IsDevelopment() ? exception.ToString() : null
        }, cancellationToken);
        return true;
    }
}
