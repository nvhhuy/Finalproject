using System.Reflection;
using Application.Contracts.Authorization;
using Application.Contracts.Services;
using Domains.UserAggregate;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Common.Behaviors;

/// <summary>
/// Short-circuits any MediatR request marked with <see cref="IRequireAdmin"/> when the current
/// principal is missing the <see cref="UserRoles.Admin"/> role claim. Replaces the legacy pattern
/// of re-loading the acting user from the database inside every admin command handler.
/// </summary>
/// <remarks>
/// Defence-in-depth: HTTP controllers already gate admin endpoints with the <c>AdminOnly</c>
/// policy, but this behavior makes the rule explicit at the application boundary so the same
/// commands stay safe when reused from non-HTTP entry points (Worker host, integration tests,
/// custom scripts).
/// </remarks>
public sealed class AdminAuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequireAdmin
{
    private readonly ILoggedInUserService _currentUser;
    private readonly ILogger<AdminAuthorizationBehavior<TRequest, TResponse>> _logger;

    public AdminAuthorizationBehavior(
        ILoggedInUserService currentUser,
        ILogger<AdminAuthorizationBehavior<TRequest, TResponse>> logger)
    {
        _currentUser = currentUser;
        _logger = logger;
    }

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_currentUser.IsInRole(UserRoles.Admin))
            return next();

        _logger.LogWarning(
            "Rejecting {Request}: caller {UserId} lacks the {Role} role claim required by IRequireAdmin.",
            typeof(TRequest).Name,
            _currentUser.UserId?.Value,
            UserRoles.Admin);

        TResponse forbidden = BuildForbiddenResult();
        return Task.FromResult(forbidden);
    }

    private static TResponse BuildForbiddenResult()
    {
        Type responseType = typeof(TResponse);
        if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition() != typeof(Result<,>))
            throw new InvalidOperationException(
                $"AdminAuthorizationBehavior requires handlers to return Result<,>, but {responseType} is unsupported.");

        MethodInfo? implicitOp = responseType.GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(Error) },
            modifiers: null);

        if (implicitOp is null)
            throw new InvalidOperationException(
                $"{responseType} does not expose an implicit conversion from {nameof(Error)}.");

        var error = new ForbiddenError("Administrator privileges are required.");
        return (TResponse)implicitOp.Invoke(null, new object[] { error })!;
    }
}
