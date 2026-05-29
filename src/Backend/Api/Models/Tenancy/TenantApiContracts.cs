using Api.Models;

namespace Api.Models.Tenancy;

public sealed record CreateTenantRequest(
    string Slug,
    string DisplayName,
    Guid OwnerUserId,
    string? PublicTagline,
    string? PublicDescription);

public sealed record TenantSummaryResponse(
    Guid Id,
    string Slug,
    string DisplayName,
    string Status,
    Guid OwnerUserId,
    DateTime CreatedAtUtc);

public sealed record TenantPublicResponse(
    Guid Id,
    string Slug,
    string DisplayName,
    string? PublicTagline,
    string? PublicDescription);

public sealed record InviteTenantMemberRequest(string Email, string Role = "member");

public sealed record TenantMemberResponse(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string Role,
    DateTime JoinedAtUtc);

public sealed record TenantHostResolutionResponse(
    Guid TenantId,
    string Slug,
    string DisplayName,
    string Source);

public sealed record TenantDomainResponse(
    Guid Id,
    Guid TenantId,
    string Hostname,
    string Status,
    string VerificationToken,
    string TxtRecordName,
    string TxtRecordValue,
    string? CnameTarget,
    DateTime CreatedAtUtc,
    DateTime? VerifiedAtUtc);

public sealed record AddTenantDomainRequest(string Hostname);

public sealed record UpsertTenantSsoSettingsRequest(
    bool IsEnabled,
    string OidcAuthority,
    string OidcClientId,
    string? OidcClientSecret);

public sealed record TenantSsoSettingsResponse(
    Guid Id,
    Guid TenantId,
    bool IsEnabled,
    string OidcAuthority,
    string OidcClientId,
    bool HasClientSecret,
    DateTime UpdatedAtUtc);

public sealed record TenantSsoPublicResponse(
    Guid TenantId,
    string TenantSlug,
    bool IsEnabled,
    string OidcAuthority,
    string OidcClientId);

public static class TenantApiMapping
{
    public static TenantSummaryResponse ToResponse(Application.Modules.Tenancy.TenantSummaryDto dto) =>
        new(dto.Id, dto.Slug, dto.DisplayName, dto.Status, dto.OwnerUserId, dto.CreatedAtUtc);

    public static TenantPublicResponse ToResponse(Application.Modules.Tenancy.TenantPublicDto dto) =>
        new(dto.Id, dto.Slug, dto.DisplayName, dto.PublicTagline, dto.PublicDescription);

    public static TenantHostResolutionResponse ToResponse(Application.Modules.Tenancy.TenantHostResolutionDto dto) =>
        new(dto.TenantId, dto.Slug, dto.DisplayName, dto.Source);

    public static TenantDomainResponse ToResponse(Application.Modules.Tenancy.TenantDomainDto dto) =>
        new(
            dto.Id,
            dto.TenantId,
            dto.Hostname,
            dto.Status,
            dto.VerificationToken,
            dto.TxtRecordName,
            dto.TxtRecordValue,
            dto.CnameTarget,
            dto.CreatedAtUtc,
            dto.VerifiedAtUtc);

    public static TenantSsoSettingsResponse ToResponse(Application.Modules.Tenancy.TenantSsoSettingsDto dto) =>
        new(
            dto.Id,
            dto.TenantId,
            dto.IsEnabled,
            dto.OidcAuthority,
            dto.OidcClientId,
            dto.HasClientSecret,
            dto.UpdatedAtUtc);

    public static TenantSsoPublicResponse ToResponse(Application.Modules.Tenancy.TenantSsoPublicDto dto) =>
        new(dto.TenantId, dto.TenantSlug, dto.IsEnabled, dto.OidcAuthority, dto.OidcClientId);
}
