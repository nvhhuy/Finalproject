using System.Net;
using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models;
using Api.Models.Auth;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AuthFlowIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public AuthFlowIntegrationTests(IntegrationAppFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Register_blocks_login_until_email_verified()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var email = $"user-{Guid.NewGuid():N}@test.local";
        using var register = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new
            {
                firstName = "Test",
                lastName = "User",
                userName = $"u{Guid.NewGuid():N}"[..12],
                email,
                password = IntegrationTestApp.DefaultPassword
            })
        }.WithIdempotencyKey();

        var registerResponse = await app.Client.SendAsync(register);
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = IntegrationTestApp.DefaultPassword });
        Assert.Equal(HttpStatusCode.Forbidden, loginResponse.StatusCode);
    }

    [SkippableFact]
    public async Task Register_verify_login_and_refresh_rotate_tokens()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (initial, email) = await app.RegisterVerifyAndLoginAsync();

        var refreshResponse = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = initial.RefreshToken });
        refreshResponse.EnsureSuccessStatusCode();
        var refreshed =
            await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        Assert.NotNull(refreshed);
        Assert.True(refreshed.Success);
        Assert.NotEqual(initial.RefreshToken, refreshed.Data!.RefreshToken);

        var relogin = await app.LoginAsync(email, IntegrationTestApp.DefaultPassword);
        Assert.NotEqual(Guid.Empty, relogin.UserId);
    }

    [SkippableFact]
    public async Task Forgot_and_reset_password_allows_login_with_new_password()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (_, email) = await app.RegisterVerifyAndLoginAsync();
        const string newPassword = "NewPassword456!";

        var forgotResponse = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new { email });
        forgotResponse.EnsureSuccessStatusCode();

        var (tokenId, token) = await app.RequireEmailTokensAsync(email);

        var resetResponse = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new { tokenId, token, newPassword });
        resetResponse.EnsureSuccessStatusCode();

        var auth = await app.LoginAsync(email, newPassword);
        Assert.Equal(email, auth.Email);
    }

    [SkippableFact]
    public async Task Resend_verification_sends_new_email_tokens()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var email = $"user-{Guid.NewGuid():N}@test.local";
        using var register = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new
            {
                firstName = "Test",
                lastName = "User",
                userName = $"u{Guid.NewGuid():N}"[..12],
                email,
                password = IntegrationTestApp.DefaultPassword
            })
        }.WithIdempotencyKey();
        await app.Client.SendAsync(register);

        app.EmailSink.Clear();
        var resendResponse = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/resend-verification",
            new { email });
        resendResponse.EnsureSuccessStatusCode();

        var (tokenId, token) = await app.RequireEmailTokensAsync(email);
        var verifyResponse = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new { tokenId, token });
        verifyResponse.EnsureSuccessStatusCode();
    }
}