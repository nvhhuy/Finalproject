using Domains.UserAggregate;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Persistence;

/// <summary>Ensures a single bootstrap admin exists when seeding is enabled (idempotent).</summary>
public static class DbSeeder
{
    /// <summary>Creates the configured admin user if no user with the same email or username exists.</summary>
    public static async Task SeedAdminIfMissingAsync(ApplicationDbContext db, IServiceProvider services)
    {
        SeedAdminOptions opts = services.GetRequiredService<IOptions<SeedAdminOptions>>().Value;
        IPasswordHasher<User> hasher = services.GetRequiredService<IPasswordHasher<User>>();

        string email = (opts.Email ?? "").Trim();
        string userName = (opts.UserName ?? "").Trim();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(userName))
            return;

        if (await db.Users.AnyAsync(u => u.Email == email || u.UserName == userName))
            return;

        string password = string.IsNullOrEmpty(opts.Password) ? "Temp!ChangeMe123" : opts.Password;
        User admin = User.CreateForLocalLoginPendingHash(opts.FirstName, opts.LastName, userName, email);
        admin.AssignRole(UserRoles.Admin);
        admin.SetInitialPasswordHash(hasher.HashPassword(admin, password));
        admin.MarkRegisteredAt(DateTime.UtcNow);
        admin.MarkEmailVerified(DateTime.UtcNow);
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}
