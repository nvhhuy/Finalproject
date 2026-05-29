namespace Domains.Administration;

/// <summary>Immutable admin action record for security review (append-only).</summary>
public sealed class AdminAuditLog
{
    public Guid Id { get; set; }
    public Guid ActorUserId { get; set; }

    /// <summary>Stable action key (see application constants for values).</summary>
    public string Action { get; set; } = null!;

    public Guid? TargetUserId { get; set; }
    public string? IpAddress { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}