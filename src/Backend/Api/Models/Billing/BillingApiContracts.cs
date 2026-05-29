using Application.Contracts.Billing;
using Application.Modules.Billing;

namespace Api.Models.Billing;

public sealed record PlanLimitsRequest(
    int MaxUsers,
    long MaxStorageBytes,
    IReadOnlyList<string> Modules,
    int MaxOpenSupportTickets,
    int AdminSeats,
    bool Impersonation,
    bool BackupRestore,
    int ApiRequestsPerMinute = 120,
    long MonthlyCreditGrant = 0)
{
    public PlanLimits ToPlanLimits()
    {
        return new PlanLimits
        {
            MaxUsers = MaxUsers,
            MaxStorageBytes = MaxStorageBytes,
            Modules = Modules,
            MaxOpenSupportTickets = MaxOpenSupportTickets,
            AdminSeats = AdminSeats,
            Impersonation = Impersonation,
            BackupRestore = BackupRestore,
            ApiRequestsPerMinute = ApiRequestsPerMinute,
            MonthlyCreditGrant = MonthlyCreditGrant
        };
    }
}

public sealed record CreatePlanRequest(
    string Code,
    string Name,
    string? Description,
    int PriceMonthlyCents,
    string Currency,
    int TrialDays,
    PlanLimitsRequest Limits,
    int SortOrder);

public sealed record UpdatePlanRequest(
    string Name,
    string? Description,
    int PriceMonthlyCents,
    string Currency,
    int TrialDays,
    PlanLimitsRequest Limits,
    bool IsActive,
    int SortOrder);

public sealed record PlanResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int PriceMonthlyCents,
    string Currency,
    int TrialDays,
    PlanLimitsRequest Limits,
    bool IsActive,
    int SortOrder);

public sealed record ChangeSubscriptionPlanRequest(string PlanCode);

public sealed record SubscribeToPlanRequest(
    string PlanCode,
    string TenantSlug,
    string TenantDisplayName,
    string? PublicTagline,
    string? PublicDescription);

public sealed record SubscribeToPlanResponse(
    Guid TenantId,
    string TenantSlug,
    Guid SubscriptionId,
    string SubscriptionStatus);

public sealed record SubscriptionResponse(
    Guid Id,
    Guid TenantId,
    Guid PlanId,
    string PlanCode,
    string PlanName,
    string Status,
    DateTime CurrentPeriodStartUtc,
    DateTime CurrentPeriodEndUtc,
    bool AllowsWriteAccess);

public sealed record CreateCreditPackageRequest(
    string Code,
    string Name,
    string? Description,
    int PriceCents,
    string Currency,
    long CreditsAmount,
    int SortOrder);

public sealed record UpdateCreditPackageRequest(
    string Name,
    string? Description,
    int PriceCents,
    string Currency,
    long CreditsAmount,
    bool IsActive,
    int SortOrder);

public sealed record CreditPackageResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int PriceCents,
    string Currency,
    long CreditsAmount,
    bool IsActive,
    int SortOrder);

public sealed record PaymentWebhookReceiptResponse(Guid EventId, bool Duplicate, string Status);

public sealed record TenantUsageResponse(int MemberCount, long StorageBytes, int OpenSupportTicketCount);

public sealed record TenantEntitlementsResponse(
    SubscriptionResponse Subscription,
    PlanLimitsRequest Limits,
    TenantUsageResponse Usage,
    bool AllowsWriteAccess);

public sealed record BillingInvoiceResponse(
    Guid Id,
    Guid? TenantId,
    Guid? UserId,
    string InvoiceNumber,
    string Description,
    int AmountCents,
    long? AmountCredits,
    string Currency,
    string Status,
    string Provider,
    DateTime IssuedAtUtc);

public static class BillingApiMapping
{
    public static PlanResponse ToResponse(PlanDto dto)
    {
        return new PlanResponse(
            dto.Id,
            dto.Code,
            dto.Name,
            dto.Description,
            dto.PriceMonthlyCents,
            dto.Currency,
            dto.TrialDays,
            new PlanLimitsRequest(
                dto.Limits.MaxUsers,
                dto.Limits.MaxStorageBytes,
                dto.Limits.Modules,
                dto.Limits.MaxOpenSupportTickets,
                dto.Limits.AdminSeats,
                dto.Limits.Impersonation,
                dto.Limits.BackupRestore,
                dto.Limits.ApiRequestsPerMinute,
                dto.Limits.MonthlyCreditGrant),
            dto.IsActive,
            dto.SortOrder);
    }

    public static SubscriptionResponse ToResponse(SubscriptionSummaryDto dto)
    {
        return new SubscriptionResponse(
            dto.Id,
            dto.TenantId,
            dto.PlanId,
            dto.PlanCode,
            dto.PlanName,
            dto.Status,
            dto.CurrentPeriodStartUtc,
            dto.CurrentPeriodEndUtc,
            dto.AllowsWriteAccess);
    }

    public static TenantEntitlementsResponse ToEntitlementsResponse(TenantEntitlementsDto dto)
    {
        return new TenantEntitlementsResponse(
            ToResponse(dto.Subscription),
            new PlanLimitsRequest(
                dto.Limits.MaxUsers,
                dto.Limits.MaxStorageBytes,
                dto.Limits.Modules,
                dto.Limits.MaxOpenSupportTickets,
                dto.Limits.AdminSeats,
                dto.Limits.Impersonation,
                dto.Limits.BackupRestore,
                dto.Limits.ApiRequestsPerMinute,
                dto.Limits.MonthlyCreditGrant),
            new TenantUsageResponse(dto.Usage.MemberCount, dto.Usage.StorageBytes, dto.Usage.OpenSupportTicketCount),
            dto.AllowsWriteAccess);
    }

    public static CreditPackageResponse ToResponse(CreditPackageDto dto)
    {
        return new CreditPackageResponse(
            dto.Id,
            dto.Code,
            dto.Name,
            dto.Description,
            dto.PriceCents,
            dto.Currency,
            dto.CreditsAmount,
            dto.IsActive,
            dto.SortOrder);
    }

    public static BillingInvoiceResponse ToResponse(BillingInvoiceListItemDto dto)
    {
        return new BillingInvoiceResponse(
            dto.Id,
            dto.TenantId,
            dto.UserId,
            dto.InvoiceNumber,
            dto.Description,
            dto.AmountCents,
            dto.AmountCredits,
            dto.Currency,
            dto.Status,
            dto.Provider,
            dto.IssuedAtUtc);
    }
}