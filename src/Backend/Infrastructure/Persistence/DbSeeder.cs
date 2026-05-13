using Domains.UserAggregate;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Persistence;

/// <summary>Development bootstrap: admin user and roles. Production should use controlled migrations + external seed jobs.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, IServiceProvider services)
    {
        IHostEnvironment env = services.GetRequiredService<IHostEnvironment>();
        if (!env.IsDevelopment())
            return;

        IPasswordHasher<User> hasher = services.GetRequiredService<IPasswordHasher<User>>();
        string adminEmail = "admin@localhost";
        if (await db.Users.AnyAsync(u => u.Email == adminEmail))
            return;

        User admin = User.CreateNew("Admin", "User", "admin", adminEmail, password: "Temp!ChangeMe123");
        admin.AssignRole(UserRoles.Admin);
        admin.UpdatePassword(hasher.HashPassword(admin, "Temp!ChangeMe123"));
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}
