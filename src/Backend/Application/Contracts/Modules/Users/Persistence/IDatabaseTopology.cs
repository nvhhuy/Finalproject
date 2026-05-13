namespace Application.Contracts.Modules.Users.Persistence;

/// <summary>Describes how read and write PostgreSQL endpoints relate (same host, replica, etc.).</summary>
public interface IDatabaseTopology
{
    string WriteConnectionName { get; }
    string ReadConnectionName { get; }
    bool UsesDedicatedReadEndpoint { get; }
    bool SharesPhysicalDatabaseWithRead { get; }
}
