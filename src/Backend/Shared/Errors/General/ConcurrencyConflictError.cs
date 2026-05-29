using System.Net;

namespace Shared.Errors.General;

/// <summary>Returned when a row was modified by another request since it was loaded.</summary>
public sealed class ConcurrencyConflictError : Error
{
    public ConcurrencyConflictError(string? message = null)
        : base(
            "ConcurrencyConflict",
            (int)HttpStatusCode.Conflict,
            message ?? "The record was modified by another request. Reload and retry.")
    {
    }
}