using System.Net;
using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models;
using Api.Models.Auth;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AuthIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public AuthIntegrationTests(IntegrationAppFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task Health_returns_ok()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        HttpResponseMessage response = await app.Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SkippableFact]
    public async Task Register_does_not_issue_tokens_until_email_verified()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        string email = $"user-{Guid.NewGuid():N}@test.local";
        using HttpRequestMessage register = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new
            {
                firstName = "Test",
                lastName = "User",
                userName = $"u{Guid.NewGuid():N}"[..12],
                email,
                password = IntegrationTestApp.DefaultPassword,
            }),
        }.WithIdempotencyKey();

        HttpResponseMessage registerResponse = await app.Client.SendAsync(register);
        registerResponse.EnsureSuccessStatusCode();
        ApiResponse<RegistrationResponseDto>? registerPayload =
            await registerResponse.Content.ReadFromJsonAsync<ApiResponse<RegistrationResponseDto>>();
        Assert.NotNull(registerPayload);
        Assert.True(registerPayload.Success);
        Assert.Contains("verify", registerPayload.Data!.Message, StringComparison.OrdinalIgnoreCase);

        HttpResponseMessage loginResponse = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = IntegrationTestApp.DefaultPassword });
        Assert.Equal(HttpStatusCode.Forbidden, loginResponse.StatusCode);
    }
}
