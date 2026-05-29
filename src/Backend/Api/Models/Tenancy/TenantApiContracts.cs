using Application.Modules.Tenancy;

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
    DateTime CreatedAtUtc,
    string? SubscriptionPlanCode,
    string? SubscriptionStatus);

public sealed record AssignTenantSubscriptionRequest(string PlanCode);

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
    public static TenantSummaryResponse ToResponse(TenantSummaryDto dto)
    {
        return new TenantSummaryResponse(
            dto.Id,
            dto.Slug,
            dto.DisplayName,
            dto.Status,
            dto.OwnerUserId,
            dto.CreatedAtUtc,
            dto.SubscriptionPlanCode,
            dto.SubscriptionStatus);
    }

    public static TenantPublicResponse ToResponse(TenantPublicDto dto)
    {
        return new TenantPublicResponse(dto.Id, dto.Slug, dto.DisplayName, dto.PublicTagline, dto.PublicDescription);
    }

    public static TenantHostResolutionResponse ToResponse(TenantHostResolutionDto dto)
    {
        return new TenantHostResolutionResponse(dto.TenantId, dto.Slug, dto.DisplayName, dto.Source);
    }

    public static TenantDomainResponse ToResponse(TenantDomainDto dto)
    {
        return new TenantDomainResponse(
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
    }

    public static TenantSsoSettingsResponse ToResponse(TenantSsoSettingsDto dto)
    {
        return new TenantSsoSettingsResponse(
            dto.Id,
            dto.TenantId,
            dto.IsEnabled,
            dto.OidcAuthority,
            dto.OidcClientId,
            dto.HasClientSecret,
            dto.UpdatedAtUtc);
    }

    public static TenantSsoPublicResponse ToResponse(TenantSsoPublicDto dto)
    {
        return new TenantSsoPublicResponse(dto.TenantId, dto.TenantSlug, dto.IsEnabled, dto.OidcAuthority,
            dto.OidcClientId);
    }
}