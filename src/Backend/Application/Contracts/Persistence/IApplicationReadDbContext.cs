using Domains.Administration;
using Domains.MediaAggregate;
using Domains.Notifications;
using Domains.Support;
using Domains.Billing;
using Domains.Tenancy;
using Domains.UserAggregate;
using Domains.Wallet;
using Microsoft.EntityFrameworkCore;

namespace Application.Contracts.Persistence;

/// <summary>
/// Read-only PostgreSQL surface for queries (CQRS read side). Spans every aggregate exposed to
/// query handlers — users, media, audit logs — so this lives in a module-agnostic namespace
/// rather than under any one feature module.
/// </summary>
public interface IApplicationReadDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<ExternalUserLogin> ExternalLogins { get; }
    DbSet<AdminAuditLog> AdminAuditLogs { get; }
    DbSet<MediaAsset> MediaAssets { get; }
    DbSet<UserNotification> UserNotifications { get; }
    DbSet<SupportTicket> SupportTickets { get; }
    DbSet<SupportTicketMessage> SupportTicketMessages { get; }
    DbSet<SupportTicketAttachment> SupportTicketAttachments { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<TenantMember> TenantMembers { get; }
    DbSet<TenantDomain> TenantDomains { get; }
    DbSet<TenantSsoSettings> TenantSsoSettings { get; }
    DbSet<Plan> Plans { get; }
    DbSet<CreditPackage> CreditPackages { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<PaymentWebhookEvent> PaymentWebhookEvents { get; }
    DbSet<BillingInvoice> BillingInvoices { get; }
    DbSet<PersonalWallet> PersonalWallets { get; }
    DbSet<WalletLedgerEntry> WalletLedgerEntries { get; }
}
