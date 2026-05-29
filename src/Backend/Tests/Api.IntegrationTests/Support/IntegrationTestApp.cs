using System.Net;
using System.Net.Http.Json;
using Api.Models;
using Api.Models.Auth;
using Application.Modules.Users.Administration.Queries;
using Application.Modules.Users.Profile.Queries;

namespace Api.IntegrationTests.Support;

/// <summary>Shared harness around the collection-scoped <see cref="CustomWebApplicationFactory" />.</summary>
public sealed class IntegrationTestApp
{
    public const string AdminEmail = "admin@localhost.com";
    public const string AdminPassword = "Temp!ChangeMe123";
    public const string DefaultPassword = "Password123!";

    private readonly CustomWebApplicationFactory _factory;

    private IntegrationTestApp(CustomWebApplicationFactory factory, HttpClient client)
    {
        _factory = factory;
        Client = client;
    }

    public CapturingEmailSender EmailSink => _factory.EmailSink;

    public HttpClient Client { get; }

    public static IntegrationTestApp FromFixture(IntegrationAppFixture fixture)
    {
        return new IntegrationTestApp(fixture.Factory, fixture.Factory.CreateClient());
    }

    public async Task<AuthResponseDto> LoginAsync(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        if (response.StatusCode == HttpStatusCode.Accepted)
            throw new InvalidOperationException("Login requires 2FA; use a user without 2FA in integration tests.");

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Login failed ({(int)response.StatusCode}): {body}",
                null,
                response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        Assert.NotNull(payload);
        Assert.True(payload.Success);
        Assert.NotNull(payload.Data);
        return payload.Data;
    }

    public Task<AuthResponseDto> LoginAdminAsync()
    {
        return LoginAsync(AdminEmail, AdminPassword);
    }

    public async Task<(AuthResponseDto Auth, string Email)> RegisterVerifyAndLoginAsync(string? email = null)
    {
        email ??= $"user-{Guid.NewGuid():N}@test.local";
        var userName = $"u{Guid.NewGuid():N}"[..12];

        var register = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new
            {
                firstName = "Test",
                lastName = "User",
                userName,
                email,
                password = DefaultPassword
            })
        }.WithIdempotencyKey();

        using (register)
        {
            var registerResponse = await Client.SendAsync(register);
            registerResponse.EnsureSuccessStatusCode();
        }

        var (tokenId, token) = await RequireEmailTokensAsync(email);

        var verifyResponse = await Client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new { tokenId, token });
        verifyResponse.EnsureSuccessStatusCode();

        var auth = await LoginAsync(email, DefaultPassword);
        return (auth, email);
    }

    public Task<(string TokenId, string Token)> RequireEmailTokensAsync(string expectedEmail)
    {
        Assert.Equal(expectedEmail, EmailSink.LastTo);
        (string tokenId, string token)? parsed = EmailBodyParser.TryParseTokenPair(EmailSink.LastBody);
        Assert.NotNull(parsed);
        return Task.FromResult(parsed.Value);
    }

    public async Task<AdminUserDetailDto> GetAdminUserDetailAsync(Guid userId, string adminAccessToken)
    {
        using var admin = _factory.CreateClient().WithBearer(adminAccessToken);
        var response = await admin.GetAsync($"/api/v1/admin/users/{userId}");
        var payload = await response.ReadApiAllowFailureAsync<AdminUserDetailDto>();
        response.EnsureSuccessStatusCode();
        Assert.NotNull(payload);
        Assert.True(payload.Success);
        Assert.NotNull(payload.Data);
        return payload.Data;
    }

    public async Task<Guid> GetCurrentUserIdAsync(string accessToken)
    {
        using var client = _factory.CreateClient().WithBearer(accessToken);
        var response = await client.GetAsync("/api/v1/users/profile");
        response.EnsureSuccessStatusCode();
        var payload = await response.ReadApiAsync<UserProfileDto>();
        Assert.NotNull(payload?.Data);
        return payload.Data.Id;
    }
}