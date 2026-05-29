using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Application.Modules.Users.Profile.Queries;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class UsersIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public UsersIntegrationTests(IntegrationAppFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Profile_get_and_update_with_row_version()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (auth, _) = await app.RegisterVerifyAndLoginAsync();
        using var userClient = app.Client.WithBearer(auth.AccessToken);

        var getResponse = await userClient.GetAsync("/api/v1/users/profile");
        var profile = await getResponse.ReadApiAllowFailureAsync<UserProfileDto>();
        getResponse.EnsureSuccessStatusCode();
        Assert.NotNull(profile?.Data);

        var putResponse = await userClient.PutAsJsonAsync(
            "/api/v1/users/profile",
            new
            {
                firstName = "Updated",
                lastName = "Name",
                rowVersion = profile.Data.RowVersion
            });
        putResponse.EnsureSuccessStatusCode();
        var updated = await putResponse.ReadApiAllowFailureAsync<object?>();
        Assert.True(updated?.Success);

        var getAgain = await userClient.GetAsync("/api/v1/users/profile");
        var after = await getAgain.ReadApiAsync<UserProfileDto>();
        Assert.Equal("Updated", after!.Data!.FirstName);
        Assert.Equal("Name", after.Data.LastName);
        Assert.True(after.Data.RowVersion > profile.Data.RowVersion);
    }
}