namespace Application.Contracts.Transactions;

/// <summary>Thrown when EF Core detects an optimistic concurrency violation on save.</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(Exception innerException)
        : base("A concurrency conflict was detected while saving changes.", innerException)
    {
    }
}