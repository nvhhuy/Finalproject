namespace Api.IntegrationTests;

/// <summary>One API host + one data-protection key ring per test collection (xUnit collection fixture).</summary>
public sealed class IntegrationAppFixture : IAsyncLifetime
{
    public PostgresFixture Postgres { get; } = new();

    public CustomWebApplicationFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Postgres.InitializeAsync();
        if (!Postgres.IsAvailable)
            return;

        Factory = new CustomWebApplicationFactory(Postgres.ConnectionString, Postgres.DataProtectionKeysDirectory);
        await Factory.MigrateAsync();
        _ = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();
        await Postgres.DisposeAsync();
    }
}