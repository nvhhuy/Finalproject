using Application.Contracts.Billing;
using Domains.Billing;
using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Domains.UserAggregate;
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
    /// <summary>
    ///     Default password applied when <see cref="SeedAdminOptions.Password" /> is empty. Documented in
    ///     <c>DEVELOPER_GUIDE.md</c>.
    /// </summary>
    public const string DefaultDevPassword = "Temp!ChangeMe123";

    /// <summary>Creates the configured admin user if no user with the same email or username exists.</summary>
    public static async Task SeedAdminIfMissingAsync(ApplicationDbContext db, IServiceProvider services)
    {
        var opts = services.GetRequiredService<IOptions<SeedAdminOptions>>().Value;
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        var env = services.GetService<IHostEnvironment>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        var email = (opts.Email ?? "").Trim();
        var userName = (opts.UserName ?? "").Trim();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(userName))
        {
            logger.LogWarning("Admin seeding skipped: Seed:Admin:Email or Seed:Admin:UserName is empty.");
            return;
        }

        if (await db.Users.AnyAsync(u => u.Email == email || u.UserName == userName))
            return;

        var passwordWasDefault = string.IsNullOrEmpty(opts.Password);
        var password = passwordWasDefault ? DefaultDevPassword : opts.Password;

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

        var admin = User.CreateForLocalLoginPendingHash(opts.FirstName, opts.LastName, userName, email);
        admin.AssignRole(UserRoles.Admin);
        admin.SetInitialPasswordHash(hasher.HashPassword(admin, password));
        admin.MarkRegisteredAt(DateTime.UtcNow);
        admin.MarkEmailVerified(DateTime.UtcNow);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        if (passwordWasDefault)
            logger.LogWarning(
                "Seeded bootstrap admin '{Email}' with the DEV default password. Change it immediately via the admin profile endpoint.",
                email);
    }

    /// <summary>
    ///     Ensures the system default tenant exists and links the bootstrap admin as owner when present.
    ///     Existing rows are backfilled by migration; this covers fresh databases after migrate.
    /// </summary>
    public static async Task SeedDefaultTenantIfMissingAsync(ApplicationDbContext db, IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var defaultId = TenantId.Create(SystemTenants.DefaultTenantId);

        if (!await db.Tenants.AnyAsync(t => t.Id == defaultId))
        {
            var ownerId = await db.Users
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

            var now = DateTime.UtcNow;
            var tenant = Tenant.CreateExisting(
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
            logger.LogInformation("Seeded default tenant '{Slug}' ({TenantId}).", SystemTenants.DefaultSlug,
                defaultId.Value);
        }
    }

    public static async Task SeedDefaultPlansIfMissingAsync(ApplicationDbContext db, IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        if (await db.Plans.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        var starterLimits = new PlanLimits
        {
            MaxUsers = 5,
            MaxStorageBytes = 2_147_483_648,
            Modules = [BillingModules.Support, BillingModules.Media],
            MaxOpenSupportTickets = 20,
            ApiRequestsPerMinute = 120,
            MonthlyCreditGrant = 0
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
            MonthlyCreditGrant = 500
        };

        db.Plans.Add(Plan.Create("starter", "Starter", "For small teams getting started.", 2900, "USD", 14,
            starterLimits.ToJson(), 10, now));
        db.Plans.Add(Plan.Create("pro", "Professional", "For growing organizations.", 9900, "USD", 14,
            proLimits.ToJson(), 20, now));
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded default billing plans (starter, pro).");
    }

    /// <summary>Backfills plan limit JSON when new fields are added (idempotent).</summary>
    public static async Task EnsureDefaultPlanLimitsAsync(ApplicationDbContext db, IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var pro = await db.Plans.FirstOrDefaultAsync(p => p.Code == "pro");
        if (pro is null)
            return;

        var limits = PlanLimits.Parse(pro.LimitsJson);
        if (limits.MonthlyCreditGrant >= 500)
            return;

        limits = new PlanLimits
        {
            MaxUsers = limits.MaxUsers > 0 ? limits.MaxUsers : 25,
            MaxStorageBytes = limits.MaxStorageBytes > 0 ? limits.MaxStorageBytes : 10_737_418_240,
            Modules = limits.Modules.Count > 0
                ? limits.Modules
                : [BillingModules.Support, BillingModules.Media, BillingModules.Notifications],
            MaxOpenSupportTickets = limits.MaxOpenSupportTickets > 0 ? limits.MaxOpenSupportTickets : 100,
            AdminSeats = limits.AdminSeats > 0 ? limits.AdminSeats : 1,
            Impersonation = limits.Impersonation,
            BackupRestore = limits.BackupRestore,
            ApiRequestsPerMinute = limits.ApiRequestsPerMinute > 0 ? limits.ApiRequestsPerMinute : 600,
            MonthlyCreditGrant = 500
        };
        pro.Update(
            pro.Name,
            pro.Description,
            pro.PriceMonthlyCents,
            pro.Currency,
            pro.TrialDays,
            limits.ToJson(),
            pro.IsActive,
            pro.SortOrder,
            DateTime.UtcNow);
        await db.SaveChangesAsync();
        logger.LogInformation("Updated pro plan limits (monthly credit grant backfill).");
    }

    public static async Task SeedDefaultCreditPackagesIfMissingAsync(ApplicationDbContext db, IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        if (await db.CreditPackages.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        db.CreditPackages.Add(CreditPackage.Create(
            "starter-500",
            "Starter pack",
            "500 credits for light usage.",
            499,
            "USD",
            500,
            10,
            now));
        db.CreditPackages.Add(CreditPackage.Create(
            "value-1500",
            "Value pack",
            "1,500 credits — best value for regular users.",
            1299,
            "USD",
            1500,
            20,
            now));
        db.CreditPackages.Add(CreditPackage.Create(
            "pack-1k-vnd",
            "1,000 credits (VND)",
            "1,000 credits — priced in Vietnamese đồng.",
            100_000,
            "VND",
            1000,
            30,
            now));
        db.CreditPackages.Add(CreditPackage.Create(
            "pack-5k-vnd",
            "5,500 credits (VND)",
            "5,500 credits with bonus — priced in Vietnamese đồng.",
            450_000,
            "VND",
            5500,
            40,
            now));
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded default credit packages.");
    }
}