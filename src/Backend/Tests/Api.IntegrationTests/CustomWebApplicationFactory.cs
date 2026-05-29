using Api.IntegrationTests.Support;
using Application.Contracts.Email;
using Application.Contracts.Persistence;
using Application.Contracts.Storage;
using Infrastructure.Persistence;
using Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _dataProtectionKeysDirectory;

    public CustomWebApplicationFactory(string connectionString, string dataProtectionKeysDirectory)
    {
        _connectionString = connectionString;
        _dataProtectionKeysDirectory = dataProtectionKeysDirectory;
    }

    public CapturingEmailSender EmailSink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("DataProtection:KeysDirectory", _dataProtectionKeysDirectory);
        builder.UseSetting("IntegrationTesting:DisableRateLimiting", "true");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PasswordReset:PublicAppBaseUrl"] = "",
                ["Seed:Admin:Enabled"] = "true",
                ["Seed:Admin:Email"] = IntegrationTestApp.AdminEmail,
                ["Seed:Admin:UserName"] = "admin",
                ["Seed:Admin:Password"] = IntegrationTestApp.AdminPassword,
                ["Seed:Admin:FirstName"] = "Admin",
                ["Seed:Admin:LastName"] = "User",
                ["Messaging:RabbitMq:Enabled"] = "false",
                ["DataProtection:KeysDirectory"] = _dataProtectionKeysDirectory,
                ["Tenancy:SubdomainBaseHost"] = "app.local",
                ["Tenancy:PlatformCnameTarget"] = "app.local",
                ["IntegrationTesting:DisableRateLimiting"] = "true",
                // Force in-memory distributed cache so tests are self-contained
                // and don't require a live Redis instance.
                ["Redis:ConnectionString"] = "",
                ["Realtime:UseRedisBackplane"] = "false"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<ApplicationReadDbContext>();
            services.RemoveAll<IApplicationReadDbContext>();
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<IObjectStorage>();

            // Swap out Redis distributed cache for an in-memory one so tests
            // are self-contained and do not require a live Redis instance.
            // ConfigureAppConfiguration overrides are too late (AddCaching already
            // ran in Program.cs), so we replace the registration here instead.
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();

            services.AddSingleton<IEmailSender>(EmailSink);
            services.AddSingleton<IObjectStorage, InMemoryObjectStorage>();

            services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(_connectionString));
            services.AddDbContext<ApplicationReadDbContext>(o => o.UseNpgsql(_connectionString));
            services.AddScoped<IApplicationReadDbContext>(sp => sp.GetRequiredService<ApplicationReadDbContext>());
        });
    }

    public async Task MigrateAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        // Avoid loading encrypted columns through EF when another machine's key ring wrote them.
        await db.Database.ExecuteSqlRawAsync("""UPDATE "Users" SET "TwoFactorSecret" = NULL""");
        await db.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Users" WHERE "Email" = {0}""",
            IntegrationTestApp.AdminEmail);

        await DbSeeder.SeedAdminIfMissingAsync(db, scope.ServiceProvider);
        await DbSeeder.SeedDefaultTenantIfMissingAsync(db, scope.ServiceProvider);
        await DbSeeder.SeedDefaultPlansIfMissingAsync(db, scope.ServiceProvider);
        await DbSeeder.EnsureDefaultPlanLimitsAsync(db, scope.ServiceProvider);
        await DbSeeder.SeedDefaultCreditPackagesIfMissingAsync(db, scope.ServiceProvider);
    }
}