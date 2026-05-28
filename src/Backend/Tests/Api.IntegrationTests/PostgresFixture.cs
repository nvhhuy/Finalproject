using Testcontainers.PostgreSql;

namespace Api.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    public const string SkipReason = "Docker or TEST_DATABASE_CONNECTION_STRING is required.";

    private PostgreSqlContainer? _container;

    public bool IsAvailable { get; private set; }

    public string ConnectionString { get; private set; } = "";

    /// <summary>Shared key ring so every WebApplicationFactory in this collection can read the same encrypted columns.</summary>
    public string DataProtectionKeysDirectory { get; private set; } = "";

    public async Task InitializeAsync()
    {
        DataProtectionKeysDirectory = Path.Combine(
            Path.GetTempPath(),
            "finalproject-api-integration-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DataProtectionKeysDirectory);
        string? external = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(external))
        {
            ConnectionString = external;
            IsAvailable = true;
            return;
        }

        try
        {
            _container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            IsAvailable = true;
        }
        catch (Exception)
        {
            IsAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationAppFixture>
{
    public const string Name = "Integration";
}
