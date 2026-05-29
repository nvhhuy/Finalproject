using System.Net;
using Api.IntegrationTests.Support;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class PlatformIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public PlatformIntegrationTests(IntegrationAppFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Health_live_returns_ok()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var response = await app.Client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SkippableFact]
    public async Task Health_ready_returns_ok_when_database_is_up()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var response = await app.Client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}