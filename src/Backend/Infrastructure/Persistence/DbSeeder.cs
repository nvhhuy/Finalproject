using Application.Contracts.Billing;
using Domains.Billing;
using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Persistence;

/// <summary>Ensures a single bootstrap admin exists when seeding is enabled (idempotent).</summary>
public static class DbSeeder
{
    /// <summary>Default password applied when <see cref="SeedAdminOptions.Password"/> is empty. Documented in <c>DEVELOPER_GUIDE.md</c>.</summary>
    public const string DefaultDevPassword = "Temp!ChangeMe123";

    /// <summary>Creates the configured admin user if no user with the same email or username exists.</summary>
    public static async Task SeedAdminIfMissingAsync(ApplicationDbContext db, IServiceProvider services)
    {
        SeedAdminOptions opts = services.GetRequiredService<IOptions<SeedAdminOptions>>().Value;
        IPasswordHasher<User> hasher = services.GetRequiredService<IPasswordHasher<User>>();
        IHostEnvironment? env = services.GetService<IHostEnvironment>();
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        string email = (opts.Email ?? "").Trim();
        string userName = (opts.UserName ?? "").Trim();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(userName))
        {
            logger.LogWarning("Admin seeding skipped: Seed:Admin:Email or Seed:Admin:UserName is empty.");
            return;
        }

        if (await db.Users.AnyAsync(u => u.Email == email || u.UserName == userName))
            return;

        bool passwordWasDefault = string.IsNullOrEmpty(opts.Password);
        string password = passwordWasDefault ? DefaultDevPassword : opts.Password;

        // Defensive guard for L12: do not silently bootstrap a production admin with the
        // dev-default password. Operators who genuinely want this must opt in by setting
        // Seed:Admin:Password explicitly (preferably via dotnet user-secrets or a vault).
        if (passwordWasDefault && env is not null && !env.IsDevelopment())
        {
            logger.LogError(
                "Refusing to seed admin '{Email}' in environment '{Env}' because Seed:Admin:Password is empty. " +
                "Set the password via configuration or user-secrets and redeploy; the default dev password is unsafe in production.",
                email, env.EnvironmentName);
            return;
        }

        User admin = User.CreateForLocalLoginPendingHash(opts.FirstName, opts.LastName, userName, email);
        admin.AssignRole(UserRoles.Admin);
        admin.SetInitialPasswordHash(hasher.HashPassword(admin, password));
        admin.MarkRegisteredAt(DateTime.UtcNow);
        admin.MarkEmailVerified(DateTime.UtcNow);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        if (passwordWasDefault)
        {
            logger.LogWarning(
                "Seeded bootstrap admin '{Email}' with the DEV default password. Change it immediately via the admin profile endpoint.",
                email);
        }
    }

    /// <summary>
    /// Ensures the system default tenant exists and links the bootstrap admin as owner when present.
    /// Existing rows are backfilled by migration; this covers fresh databases after migrate.
    /// </summary>
    public static async Task SeedDefaultTenantIfMissingAsync(ApplicationDbContext db, IServiceProvider services)
    {
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        TenantId defaultId = TenantId.Create(SystemTenants.DefaultTenantId);

        if (!await db.Tenants.AnyAsync(t => t.Id == defaultId))
        {
        UserId? ownerId = await db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRoles.Admin)
            .OrderBy(u => u.RegisteredAtUtc)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (ownerId is null || ownerId.Value == Guid.Empty)
            {
                logger.LogWarning("Default tenant seed skipped: no admin user exists yet.");
                return;
            }

            DateTime now = DateTime.UtcNow;
            Tenant tenant = Tenant.CreateExisting(
                defaultId,
                SystemTenants.DefaultSlug,
                SystemTenants.DefaultDisplayName,
                ownerId,
                TenantStatus.Active,
                now,
                now);
            tenant.AddMember(ownerId, TenantMemberRoles.Owner, now);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded default tenant '{Slug}' ({TenantId}).", SystemTenants.DefaultSlug, defaultId.Value);
        }
    }

    public static async Task SeedDefaultPlansIfMissingAsync(ApplicationDbContext db, IServiceProvider services)
    {
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        if (await db.Plans.AnyAsync())
            return;

        DateTime now = DateTime.UtcNow;
        var starterLimits = new PlanLimits
        {
            MaxUsers = 5,
            MaxStorageBytes = 2_147_483_648,
            Modules = [BillingModules.Support, BillingModules.Media],
            MaxOpenSupportTickets = 20,
            ApiRequestsPerMinute = 120,
            MonthlyCreditGrant = 0,
        };
        var proLimits = new PlanLimits
        {
            MaxUsers = 25,
            MaxStorageBytes = 10_737_418_240,
            Modules = [BillingModules.Support, BillingModules.Media, BillingModules.Notifications],
            MaxOpenSupportTickets = 100,
            Impersonation = true,
            BackupRestore = true,
            ApiRequestsPerMinute = 600,
            MonthlyCreditGrant = 500,
        };

        db.Plans.Add(Plan.Create("starter", "Starter", "For small teams getting started.", 2900, "USD", 14, starterLimits.ToJson(), 10, now));
        db.Plans.Add(Plan.Create("pro", "Professional", "For growing organizations.", 9900, "USD", 14, proLimits.ToJson(), 20, now));
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded default billing plans (starter, pro).");
    }
}
