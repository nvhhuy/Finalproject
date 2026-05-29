using System.Net;
using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models.Billing;
using Api.Models.Tenancy;
using Api.Tenancy;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class TenancySsoIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public TenancySsoIntegrationTests(IntegrationAppFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Tenant_owner_can_configure_sso_and_public_slug_exposes_oidc_metadata()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (ownerAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using var ownerClient = app.Client.WithBearer(ownerAuth.AccessToken);

        var slug = $"sso-{Guid.NewGuid():N}"[..18];
        var subscribeResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest("starter", slug, "SSO Org", null, null));
        subscribeResponse.EnsureSuccessStatusCode();
        var subscribed =
            await subscribeResponse.ReadApiAsync<SubscribeToPlanResponse>();
        Assert.NotNull(subscribed?.Data);
        var tenantId = subscribed.Data.TenantId;

        ownerClient.DefaultRequestHeaders.Add(TenantHttpHeaders.TenantId, tenantId.ToString());

        var upsertResponse = await ownerClient.PutAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/sso",
            new UpsertTenantSsoSettingsRequest(
                true,
                "https://login.microsoftonline.com/common/v2.0",
                "integration-client-id",
                "integration-client-secret"));
        upsertResponse.EnsureSuccessStatusCode();
        var settings =
            await upsertResponse.ReadApiAsync<TenantSsoSettingsResponse>();
        Assert.NotNull(settings?.Data);
        Assert.True(settings.Data.IsEnabled);
        Assert.True(settings.Data.HasClientSecret);

        var publicResponse = await app.Client.GetAsync($"/api/v1/public/tenants/{slug}/sso");
        publicResponse.EnsureSuccessStatusCode();
        var publicSso =
            await publicResponse.ReadApiAsync<TenantSsoPublicResponse>();
        Assert.NotNull(publicSso?.Data);
        Assert.Equal(tenantId, publicSso.Data.TenantId);
        Assert.Equal("integration-client-id", publicSso.Data.OidcClientId);
    }

    [SkippableFact]
    public async Task Non_owner_cannot_configure_tenant_sso()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (ownerAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using var ownerClient = app.Client.WithBearer(ownerAuth.AccessToken);

        var slug = $"sso2-{Guid.NewGuid():N}"[..18];
        var subscribeResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest("starter", slug, "SSO Org 2", null, null));
        var subscribed =
            await subscribeResponse.ReadApiAsync<SubscribeToPlanResponse>();
        var tenantId = subscribed!.Data!.TenantId;

        var (otherAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using var otherClient = app.Client.WithBearer(otherAuth.AccessToken);
        otherClient.DefaultRequestHeaders.Add(TenantHttpHeaders.TenantId, tenantId.ToString());

        var upsertResponse = await otherClient.PutAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/sso",
            new UpsertTenantSsoSettingsRequest(
                true,
                "https://accounts.google.com",
                "other-client",
                null));

        Assert.Equal(HttpStatusCode.Forbidden, upsertResponse.StatusCode);
    }
}