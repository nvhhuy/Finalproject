using Application.Contracts.Persistence;
using Application.Contracts.Tenancy;
using Domains.Administration;
using Domains.Billing;
using Domains.Contact;
using Domains.MediaAggregate;
using Domains.Notifications;
using Domains.Support;
using Domains.SystemSettings;
using Domains.Tenancy;
using Domains.UserAggregate;
using Domains.Wallet;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
///     CQRS read-side DbContext. Tracking and write paths are disabled — calling <c>SaveChanges</c>
///     (sync or async) throws <see cref="InvalidOperationException" /> so accidental writes through
///     the read context fail fast at runtime.
/// </summary>
public sealed class ApplicationReadDbContext : DbContext, IApplicationReadDbContext
{
    private const string WriteForbiddenMessage =
        "ApplicationReadDbContext is read-only. Use ApplicationDbContext (write side) and IUnitOfWork.SaveChangesAsync for mutations.";

    private readonly ITenantContext _tenantContext;

    public ApplicationReadDbContext(
        DbContextOptions<ApplicationReadDbContext> options,
        ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
        SavingChanges += (_, _) => throw new InvalidOperationException(WriteForbiddenMessage);
    }

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ExternalUserLogin> ExternalLogins => Set<ExternalUserLogin>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketMessage> SupportTicketMessages => Set<SupportTicketMessage>();
    public DbSet<SupportTicketAttachment> SupportTicketAttachments => Set<SupportTicketAttachment>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMember> TenantMembers => Set<TenantMember>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
    public DbSet<TenantSsoSettings> TenantSsoSettings => Set<TenantSsoSettings>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<CreditPackage> CreditPackages => Set<CreditPackage>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<BillingInvoice> BillingInvoices => Set<BillingInvoice>();
    public DbSet<PersonalWallet> PersonalWallets => Set<PersonalWallet>();
    public DbSet<WalletLedgerEntry> WalletLedgerEntries => Set<WalletLedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        PersistenceModel.Configure(modelBuilder);
        TenantQueryFilterConfigurator.Apply(modelBuilder, _tenantContext);
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        throw new InvalidOperationException(WriteForbiddenMessage);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        throw new InvalidOperationException(WriteForbiddenMessage);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(WriteForbiddenMessage);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(WriteForbiddenMessage);
    }
}