namespace Application.Contracts.Services;

/// <summary>
///     Ambient correlation identifier for the current execution scope. Set by the API's
///     <c>CorrelationIdMiddleware</c> at the entrance of every request, and re-set by message
///     consumers when they pick up a queued event. Application/infra code that needs to stamp a
///     correlation id onto a downstream artefact (outbox row, log scope, outbound HTTP header)
///     reads it from here instead of taking a hard dependency on <c>HttpContext</c>.
/// </summary>
/// <remarks>
///     Implementation backs onto <see cref="System.Threading.AsyncLocal{T}" /> so the value flows
///     across async/await boundaries inside the same logical operation. Background hosts that
///     process queued messages must explicitly set the value at the start of every handler scope —
///     AsyncLocal does NOT carry between unrelated tasks.
/// </remarks>
public interface ICorrelationContext
{
    /// <summary>Current correlation id, or <c>null</c> when no scope has been entered.</summary>
    string? Current { get; }

    /// <summary>
    ///     Sets the correlation id for the calling logical context and returns a disposable that
    ///     restores the previous value. Use with <c>using</c> so nested scopes (e.g. a child
    ///     invocation triggered by a webhook callback) don't leak ids into peer requests.
    /// </summary>
    IDisposable BeginScope(string correlationId);
}