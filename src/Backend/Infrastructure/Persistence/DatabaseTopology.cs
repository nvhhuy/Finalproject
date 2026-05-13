using Application.Contracts.Modules.Users.Persistence;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

/// <summary>
/// Describes how PostgreSQL read and write connection strings relate. Same normalized connection means a single
/// database (typical dev); different values imply a replica or second cluster — same EF migrations must be applied to both.
/// </summary>
public sealed class DatabaseTopology : IDatabaseTopology
{
    public DatabaseTopology(IConfiguration configuration)
    {
        string? write = configuration.GetConnectionString("DatabaseConnectionString");
        string? read = configuration.GetConnectionString("DatabaseReadConnectionString");
        if (string.IsNullOrWhiteSpace(read))
            read = write;

        WriteConnectionName = "DatabaseConnectionString";
        ReadConnectionName = "DatabaseReadConnectionString";
        UsesDedicatedReadEndpoint = !string.Equals(Normalize(write), Normalize(read), StringComparison.Ordinal);
        SharesPhysicalDatabaseWithRead = !UsesDedicatedReadEndpoint;
    }

    public string WriteConnectionName { get; }
    public string ReadConnectionName { get; }
    public bool UsesDedicatedReadEndpoint { get; }
    public bool SharesPhysicalDatabaseWithRead { get; }

    private static string? Normalize(string? cs)
    {
        if (string.IsNullOrWhiteSpace(cs))
            return null;
        IEnumerable<string> parts = cs.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !p.StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
        return string.Join(";", parts);
    }
}
