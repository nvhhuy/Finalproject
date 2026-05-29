using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models.Billing;
using Api.Models.Tenancy;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class TenantHostIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public TenantHostIntegrationTests(IntegrationAppFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Resolve_host_maps_tenant_subdomain()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (ownerAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using var client = app.Client.WithBearer(ownerAuth.AccessToken);

        var slug = $"sub-{Guid.NewGuid():N}"[..18];
        var subscribeResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest("starter", slug, "Subdomain Org", null, null));
        subscribeResponse.EnsureSuccessStatusCode();

        var host = $"{slug}.app.local";
        var resolveResponse = await app.Client.GetAsync(
            $"/api/v1/public/tenants/resolve-host?host={Uri.EscapeDataString(host)}");
        resolveResponse.EnsureSuccessStatusCode();
        var resolved =
            await resolveResponse.ReadApiAsync<TenantHostResolutionResponse>();
        Assert.NotNull(resolved?.Data);
        Assert.Equal(slug, resolved.Data.Slug);
        Assert.Equal("Subdomain", resolved.Data.Source);
    }

    [SkippableFact]
    public async Task Owner_can_register_custom_domain()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (ownerAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using var client = app.Client.WithBearer(ownerAuth.AccessToken);

        var slug = $"dom-{Guid.NewGuid():N}"[..18];
        var subscribeResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest("starter", slug, "Domain Org", null, null));
        subscribeResponse.EnsureSuccessStatusCode();
        var subscribed =
            await subscribeResponse.ReadApiAsync<SubscribeToPlanResponse>();
        Assert.NotNull(subscribed?.Data);

        var hostname = $"portal-{Guid.NewGuid():N}.example.test";
        var addResponse = await client.PostAsJsonAsync(
            $"/api/v1/tenants/{subscribed.Data.TenantId}/domains",
            new AddTenantDomainRequest(hostname));
        addResponse.EnsureSuccessStatusCode();
        var domain = await addResponse.ReadApiAsync<TenantDomainResponse>();
        Assert.NotNull(domain?.Data);
        Assert.Equal(hostname, domain.Data.Hostname);
        Assert.Equal("PendingVerification", domain.Data.Status);
    }
}