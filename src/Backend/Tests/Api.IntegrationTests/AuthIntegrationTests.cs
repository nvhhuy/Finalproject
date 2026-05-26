using System.Net;
using System.Net.Http.Json;
using Api.Models;
using Api.Models.Auth;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AuthIntegrationTests
{
    private readonly PostgresFixture _postgres;

    public AuthIntegrationTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Health_returns_ok()
    {
        Skip.IfNot(_postgres.IsAvailable, "Docker or TEST_DATABASE_CONNECTION_STRING is required.");
        using CustomWebApplicationFactory factory = new(_postgres.ConnectionString);
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SkippableFact]
    public async Task Register_does_not_issue_tokens_until_email_verified()
    {
        Skip.IfNot(_postgres.IsAvailable, "Docker or TEST_DATABASE_CONNECTION_STRING is required.");
        using CustomWebApplicationFactory factory = new(_postgres.ConnectionString);
        await factory.MigrateAsync();

        HttpClient client = factory.CreateClient();
        string email = $"user-{Guid.NewGuid():N}@test.local";
        var registerBody = new
        {
            firstName = "Test",
            lastName = "User",
            userName = $"u{Guid.NewGuid():N}"[..12],
            email,
            password = "Password123!"
        };

        HttpResponseMessage registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerBody);
        registerResponse.EnsureSuccessStatusCode();
        ApiResponse<RegistrationResponseDto>? registerPayload =
            await registerResponse.Content.ReadFromJsonAsync<ApiResponse<RegistrationResponseDto>>();
        Assert.NotNull(registerPayload);
        Assert.True(registerPayload.Success);
        Assert.Contains("verify", registerPayload.Data!.Message, StringComparison.OrdinalIgnoreCase);

        HttpResponseMessage loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password123!" });
        Assert.Equal(HttpStatusCode.Forbidden, loginResponse.StatusCode);
    }
}
