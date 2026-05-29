using Domains.Administration;
using Domains.MediaAggregate;
using Domains.Notifications;
using Domains.Support;
using Domains.Billing;
using Domains.Tenancy;
using Domains.Wallet;
using Domains.UserAggregate;
using Domains.SystemSettings;
using Infrastructure.Idempotency;
using Infrastructure.Messaging.Outbox;
using Infrastructure.Persistence.Interceptors;
using Application.Contracts.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// CQRS write-side DbContext. Tracking enabled. Use <c>IUnitOfWork.SaveChangesAsync</c> from
/// command handlers — do not call <c>SaveChanges</c> directly on this context.
/// </summary>
public sealed class ApplicationDbContext : DbContext
{
    private readonly DomainEventsSaveChangesInterceptor _domainEventsInterceptor;
    private readonly ITenantContext _tenantContext;

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ExternalUserLogin> ExternalLogins => Set<ExternalUserLogin>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketMessage> SupportTicketMessages => Set<SupportTicketMessage>();
    public DbSet<SupportTicketAttachment> SupportTicketAttachments => Set<SupportTicketAttachment>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMember> TenantMembers => Set<TenantMember>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
    public DbSet<TenantSsoSettings> TenantSsoSettings => Set<TenantSsoSettings>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<BillingInvoice> BillingInvoices => Set<BillingInvoice>();
    public DbSet<PersonalWallet> PersonalWallets => Set<PersonalWallet>();
    public DbSet<WalletLedgerEntry> WalletLedgerEntries => Set<WalletLedgerEntry>();

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        DomainEventsSaveChangesInterceptor domainEventsInterceptor,
        ITenantContext tenantContext) : base(options)
    {
        _domainEventsInterceptor = domainEventsInterceptor;
        _tenantContext = tenantContext;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_domainEventsInterceptor);
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        PersistenceModel.Configure(modelBuilder);
        TenantQueryFilterConfigurator.Apply(modelBuilder, _tenantContext);
        base.OnModelCreating(modelBuilder);
    }
}
