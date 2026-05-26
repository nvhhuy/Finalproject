using System.Reflection;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Common.Behaviors;

/// <summary>
/// Runs every <see cref="IValidator{TRequest}"/> registered for the current MediatR request and
/// short-circuits with a <see cref="ValidationError"/> if any rule fails. Replaces the ad-hoc
/// pattern of calling <c>request.ValidateAsync(new XxxValidator())</c> inside each handler.
/// </summary>
/// <remarks>
/// Like <c>AdminAuthorizationBehavior</c>, this behavior assumes handlers return
/// <see cref="Result{TSuccess,TError}"/>; failure injection uses the implicit conversion from
/// <see cref="Error"/>. Requests without a registered validator pass through untouched.
/// </remarks>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        ValidationResult[] results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        List<FluentValidation.Results.ValidationFailure> failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        var error = new ValidationError();
        foreach (FluentValidation.Results.ValidationFailure failure in failures)
        {
            // FluentValidation can produce multiple failures per property; we keep the first message
            // per property for a stable shape compatible with the existing API contract.
            if (!error.ValidationErrorDictionary.ContainsKey(failure.PropertyName))
                error.ValidationErrorDictionary[failure.PropertyName] = failure.ErrorMessage;
        }

        return BuildValidationFailureResult(error);
    }

    private static TResponse BuildValidationFailureResult(ValidationError error)
    {
        Type responseType = typeof(TResponse);
        if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition() != typeof(Result<,>))
            throw new InvalidOperationException(
                $"ValidationBehavior requires handlers to return Result<,>, but {responseType} is unsupported.");

        MethodInfo? implicitOp = responseType.GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(Error) },
            modifiers: null);

        if (implicitOp is null)
            throw new InvalidOperationException(
                $"{responseType} does not expose an implicit conversion from {nameof(Error)}.");

        return (TResponse)implicitOp.Invoke(null, new object[] { error })!;
    }
}
