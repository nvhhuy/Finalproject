using System.Text.RegularExpressions;
using Domains.Common;
using Domains.Tenancy.ValueObjects;
using Domains.UserAggregate.ValueObjects;

namespace Domains.Tenancy;

public sealed partial class Tenant : AggregateRoot<TenantId>
{
    public string Slug { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public TenantStatus Status { get; private set; }
    public UserId OwnerUserId { get; private set; } = null!;
    public string? PublicTagline { get; private set; }
    public string? PublicDescription { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private readonly List<TenantMember> _members = [];
    public IReadOnlyList<TenantMember> Members => _members.AsReadOnly();

    private Tenant() { }

    private Tenant(
        TenantId id,
        string slug,
        string displayName,
        UserId ownerUserId,
        DateTime utcNow) : base(id)
    {
        Slug = slug;
        DisplayName = displayName;
        OwnerUserId = ownerUserId;
        Status = TenantStatus.Active;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public static Tenant Create(string slug, string displayName, UserId ownerUserId, DateTime utcNow)
    {
        string normalizedSlug = NormalizeSlug(slug);
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));
        if (ownerUserId is null || ownerUserId.Value == Guid.Empty)
            throw new ArgumentException("Owner user id is required.", nameof(ownerUserId));

        Tenant tenant = new(TenantId.CreateNew(), normalizedSlug, displayName.Trim(), ownerUserId, utcNow);
        tenant.AddMember(ownerUserId, TenantMemberRoles.Owner, utcNow);
        return tenant;
    }

    /// <summary>Rehydrates a tenant for migrations/seeds only.</summary>
    public static Tenant CreateExisting(
        TenantId id,
        string slug,
        string displayName,
        UserId ownerUserId,
        TenantStatus status,
        DateTime createdAtUtc,
        DateTime updatedAtUtc) =>
        new(id, slug, displayName, ownerUserId, createdAtUtc)
        {
            Status = status,
            UpdatedAtUtc = updatedAtUtc,
        };

    public TenantMember AddMember(UserId userId, string role, DateTime utcNow)
    {
        if (!TenantMemberRoles.IsValid(role))
            throw new ArgumentException($"Invalid tenant member role '{role}'.", nameof(role));
        if (_members.Any(m => m.UserId.Value == userId.Value))
            throw new InvalidOperationException("User is already a member of this tenant.");

        var member = TenantMember.Create(Id, userId, role, utcNow);
        _members.Add(member);
        UpdatedAtUtc = utcNow;
        return member;
    }

    public void UpdateBranding(string? tagline, string? description, DateTime utcNow)
    {
        PublicTagline = string.IsNullOrWhiteSpace(tagline) ? null : tagline.Trim();
        PublicDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAtUtc = utcNow;
    }

    public void Suspend(DateTime utcNow)
    {
        Status = TenantStatus.Suspended;
        UpdatedAtUtc = utcNow;
    }

    public void Activate(DateTime utcNow)
    {
        Status = TenantStatus.Active;
        UpdatedAtUtc = utcNow;
    }

    public static string NormalizeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Slug is required.", nameof(slug));

        string normalized = slug.Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 63)
            throw new ArgumentException("Slug must be between 3 and 63 characters.", nameof(slug));
        if (!SlugPattern().IsMatch(normalized))
            throw new ArgumentException("Slug may contain lowercase letters, digits, and hyphens only.", nameof(slug));
        if (normalized.StartsWith('-') || normalized.EndsWith('-'))
            throw new ArgumentException("Slug cannot start or end with a hyphen.", nameof(slug));

        return normalized;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
