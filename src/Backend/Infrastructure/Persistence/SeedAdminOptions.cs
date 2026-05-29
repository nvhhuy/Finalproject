namespace Infrastructure.Persistence;

/// <summary>
///     Optional first-run admin bootstrap. Bind from <c>Seed:Admin</c>. Invoked from the Api host when seeding is
///     enabled.
/// </summary>
public sealed class SeedAdminOptions
{
    public const string SectionName = "Seed:Admin";

    /// <summary>When true, ensures one admin exists (idempotent). Development seeds by default without this flag.</summary>
    public bool Enabled { get; set; }

    public string Email { get; set; } = "admin@localhost.com";
    public string UserName { get; set; } = "admin";
    public string Password { get; set; } = "Temp!ChangeMe123";
    public string FirstName { get; set; } = "Admin";
    public string LastName { get; set; } = "User";
}