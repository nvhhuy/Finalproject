using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models;
using Api.Models.Auth;
using Api.Models.Tenancy;
using Api.Tenancy;
using Domains.Tenancy;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class TenancyIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public TenancyIntegrationTests(IntegrationAppFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task Platform_admin_can_create_tenant_and_public_slug_is_readable()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);
        AuthResponseDto admin = await app.LoginAdminAsync();
        using HttpClient adminClient = app.Client.WithBearer(admin.AccessToken);

        (AuthResponseDto ownerAuth, string ownerEmail) = await app.RegisterVerifyAndLoginAsync();
        _ = ownerEmail;
        Guid ownerId = await app.GetCurrentUserIdAsync(ownerAuth.AccessToken);

        string slug = $"acme-{Guid.NewGuid():N}"[..20];
        HttpResponseMessage createResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/platform/tenants",
            new CreateTenantRequest(slug, "Acme Corp", ownerId, "We ship fast", "Tenant landing copy"));
        createResponse.EnsureSuccessStatusCode();
        ApiResponse<TenantSummaryResponse>? created = await createResponse.ReadApiAsync<TenantSummaryResponse>();
        Assert.NotNull(created?.Data);
        Assert.Equal(slug, created.Data.Slug);

        HttpResponseMessage publicResponse = await app.Client.GetAsync($"/api/v1/public/tenants/{slug}");
        publicResponse.EnsureSuccessStatusCode();
        ApiResponse<TenantPublicResponse>? pub = await publicResponse.ReadApiAsync<TenantPublicResponse>();
        Assert.NotNull(pub?.Data);
        Assert.Equal("Acme Corp", pub.Data.DisplayName);
    }

    [SkippableFact]
    public async Task Tenant_member_lists_mine_and_non_member_gets_forbidden_with_tenant_header()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);
        AuthResponseDto admin = await app.LoginAdminAsync();
        using HttpClient adminClient = app.Client.WithBearer(admin.AccessToken);

        (AuthResponseDto memberAuth, _) = await app.RegisterVerifyAndLoginAsync();
        Guid memberId = await app.GetCurrentUserIdAsync(memberAuth.AccessToken);

        string slug = $"team-{Guid.NewGuid():N}"[..20];
        HttpResponseMessage createResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/platform/tenants",
            new CreateTenantRequest(slug, "Team", memberId, null, null));
        createResponse.EnsureSuccessStatusCode();
        ApiResponse<TenantSummaryResponse>? created = await createResponse.ReadApiAsync<TenantSummaryResponse>();
        Guid tenantId = created!.Data!.Id;

        using HttpClient memberClient = app.Client.WithBearer(memberAuth.AccessToken);

        HttpResponseMessage mineResponse = await memberClient.GetAsync("/api/v1/tenants/mine");
        mineResponse.EnsureSuccessStatusCode();
        ApiResponse<IReadOnlyList<TenantSummaryResponse>>? mine =
            await mineResponse.ReadApiAsync<IReadOnlyList<TenantSummaryResponse>>();
        Assert.Contains(mine!.Data!, t => t.Id == tenantId);

        (AuthResponseDto outsiderAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using HttpClient outsiderClient = app.Client.WithBearer(outsiderAuth.AccessToken);
        outsiderClient.DefaultRequestHeaders.Add(TenantHttpHeaders.TenantId, tenantId.ToString());

        HttpResponseMessage forbidden = await outsiderClient.GetAsync("/api/v1/tenants/mine");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [SkippableFact]
    public async Task Default_tenant_is_seeded_for_bootstrap_admin()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        HttpResponseMessage publicResponse = await app.Client.GetAsync($"/api/v1/public/tenants/{SystemTenants.DefaultSlug}");
        publicResponse.EnsureSuccessStatusCode();
        ApiResponse<TenantPublicResponse>? pub = await publicResponse.ReadApiAsync<TenantPublicResponse>();
        Assert.NotNull(pub?.Data);
        Assert.Equal(SystemTenants.DefaultTenantId, pub.Data.Id);
    }
}
