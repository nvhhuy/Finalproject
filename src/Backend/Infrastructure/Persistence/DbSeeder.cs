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
}
