namespace Infrastructure.Idempotency;

public enum IdempotencyRecordStatus
{
    InProgress = 0,
    Completed = 1
}

/// <summary>Persisted idempotency outcome for a mutating HTTP request.</summary>
public sealed class IdempotencyRecord
{
    public Guid Id { get; private set; }
    public string Scope { get; private set; } = null!;
    public string OwnerPartition { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;
    public IdempotencyRecordStatus Status { get; private set; }
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    private IdempotencyRecord() { }

    public static IdempotencyRecord CreateInProgress(
        string scope,
        string ownerPartition,
        string idempotencyKey,
        DateTime nowUtc,
        TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(scope))
            throw new ArgumentException("Scope is required.", nameof(scope));
        if (string.IsNullOrWhiteSpace(ownerPartition))
            throw new ArgumentException("Owner partition is required.", nameof(ownerPartition));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));

        return new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Scope = scope.Trim(),
            OwnerPartition = ownerPartition.Trim(),
            IdempotencyKey = idempotencyKey.Trim(),
            Status = IdempotencyRecordStatus.InProgress,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.Add(ttl)
        };
    }

    public void Complete(int responseStatusCode, string responseBody)
    {
        Status = IdempotencyRecordStatus.Completed;
        ResponseStatusCode = responseStatusCode;
        ResponseBody = responseBody;
    }
}
