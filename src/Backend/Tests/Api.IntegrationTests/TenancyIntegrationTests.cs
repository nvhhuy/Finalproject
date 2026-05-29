using System.Net;
using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models.Tenancy;
using Api.Tenancy;
using Domains.Tenancy;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class TenancyIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public TenancyIntegrationTests(IntegrationAppFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Platform_admin_can_create_tenant_and_public_slug_is_readable()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);
        var admin = await app.LoginAdminAsync();
        using var adminClient = app.Client.WithBearer(admin.AccessToken);

        var (ownerAuth, ownerEmail) = await app.RegisterVerifyAndLoginAsync();
        _ = ownerEmail;
        var ownerId = await app.GetCurrentUserIdAsync(ownerAuth.AccessToken);

        var slug = $"acme-{Guid.NewGuid():N}"[..20];
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/platform/tenants",
            new CreateTenantRequest(slug, "Acme Corp", ownerId, "We ship fast", "Tenant landing copy"));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.ReadApiAsync<TenantSummaryResponse>();
        Assert.NotNull(created?.Data);
        Assert.Equal(slug, created.Data.Slug);

        var publicResponse = await app.Client.GetAsync($"/api/v1/public/tenants/{slug}");
        publicResponse.EnsureSuccessStatusCode();
        var pub = await publicResponse.ReadApiAsync<TenantPublicResponse>();
        Assert.NotNull(pub?.Data);
        Assert.Equal("Acme Corp", pub.Data.DisplayName);
    }

    [SkippableFact]
    public async Task Tenant_member_lists_mine_and_non_member_gets_forbidden_with_tenant_header()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);
        var admin = await app.LoginAdminAsync();
        using var adminClient = app.Client.WithBearer(admin.AccessToken);

        var (memberAuth, _) = await app.RegisterVerifyAndLoginAsync();
        var memberId = await app.GetCurrentUserIdAsync(memberAuth.AccessToken);

        var slug = $"team-{Guid.NewGuid():N}"[..20];
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/platform/tenants",
            new CreateTenantRequest(slug, "Team", memberId, null, null));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.ReadApiAsync<TenantSummaryResponse>();
        var tenantId = created!.Data!.Id;

        using var memberClient = app.Client.WithBearer(memberAuth.AccessToken);

        var mineResponse = await memberClient.GetAsync("/api/v1/tenants/mine");
        mineResponse.EnsureSuccessStatusCode();
        var mine =
            await mineResponse.ReadApiAsync<IReadOnlyList<TenantSummaryResponse>>();
        Assert.Contains(mine!.Data!, t => t.Id == tenantId);

        var (outsiderAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using var outsiderClient = app.Client.WithBearer(outsiderAuth.AccessToken);
        outsiderClient.DefaultRequestHeaders.Add(TenantHttpHeaders.TenantId, tenantId.ToString());

        var forbidden = await outsiderClient.GetAsync("/api/v1/tenants/mine");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [SkippableFact]
    public async Task Default_tenant_is_seeded_for_bootstrap_admin()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var publicResponse = await app.Client.GetAsync($"/api/v1/public/tenants/{SystemTenants.DefaultSlug}");
        publicResponse.EnsureSuccessStatusCode();
        var pub = await publicResponse.ReadApiAsync<TenantPublicResponse>();
        Assert.NotNull(pub?.Data);
        Assert.Equal(SystemTenants.DefaultTenantId, pub.Data.Id);
    }
}