using Api.IntegrationTests.Support;
using Application.Contracts.Email;
using Application.Contracts.Persistence;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _dataProtectionKeysDirectory;

    public CapturingEmailSender EmailSink { get; } = new();

    public CustomWebApplicationFactory(string connectionString, string dataProtectionKeysDirectory)
    {
        _connectionString = connectionString;
        _dataProtectionKeysDirectory = dataProtectionKeysDirectory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("DataProtection:KeysDirectory", _dataProtectionKeysDirectory);
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
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<ApplicationReadDbContext>();
            services.RemoveAll<IApplicationReadDbContext>();
            services.RemoveAll<IEmailSender>();

            services.AddSingleton<IEmailSender>(EmailSink);

            services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(_connectionString));
            services.AddDbContext<ApplicationReadDbContext>(o => o.UseNpgsql(_connectionString));
            services.AddScoped<IApplicationReadDbContext>(sp => sp.GetRequiredService<ApplicationReadDbContext>());
        });
    }

    public async Task MigrateAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        // Avoid loading encrypted columns through EF when another machine's key ring wrote them.
        await db.Database.ExecuteSqlRawAsync("""UPDATE "Users" SET "TwoFactorSecret" = NULL""");
        await db.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Users" WHERE "Email" = {0}""",
            IntegrationTestApp.AdminEmail);

        await DbSeeder.SeedAdminIfMissingAsync(db, scope.ServiceProvider);
        await DbSeeder.SeedDefaultTenantIfMissingAsync(db, scope.ServiceProvider);
        await DbSeeder.SeedDefaultPlansIfMissingAsync(db, scope.ServiceProvider);
    }
}
