using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models;
using Api.Models.Auth;
using Application.Modules.Users.Profile.Queries;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class UsersIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public UsersIntegrationTests(IntegrationAppFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task Profile_get_and_update_with_row_version()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        (AuthResponseDto auth, _) = await app.RegisterVerifyAndLoginAsync();
        using HttpClient userClient = app.Client.WithBearer(auth.AccessToken);

        HttpResponseMessage getResponse = await userClient.GetAsync("/api/v1/users/profile");
        ApiResponse<UserProfileDto>? profile = await getResponse.ReadApiAllowFailureAsync<UserProfileDto>();
        getResponse.EnsureSuccessStatusCode();
        Assert.NotNull(profile?.Data);

        HttpResponseMessage putResponse = await userClient.PutAsJsonAsync(
            "/api/v1/users/profile",
            new
            {
                firstName = "Updated",
                lastName = "Name",
                rowVersion = profile.Data.RowVersion,
            });
        putResponse.EnsureSuccessStatusCode();
        ApiResponse<object?>? updated = await putResponse.ReadApiAllowFailureAsync<object?>();
        Assert.True(updated?.Success);

        HttpResponseMessage getAgain = await userClient.GetAsync("/api/v1/users/profile");
        ApiResponse<UserProfileDto>? after = await getAgain.ReadApiAsync<UserProfileDto>();
        Assert.Equal("Updated", after!.Data!.FirstName);
        Assert.Equal("Name", after.Data.LastName);
        Assert.True(after.Data.RowVersion > profile.Data.RowVersion);
    }
}
