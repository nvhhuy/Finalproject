using System.Net;
using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models.Billing;
using Api.Models.Tenancy;
using Api.Models.Wallet;
using Api.Tenancy;
using Application.Modules.Billing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class BillingIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public BillingIntegrationTests(IntegrationAppFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Public_plans_lists_seeded_catalog()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var response = await app.Client.GetAsync("/api/v1/public/plans");
        response.EnsureSuccessStatusCode();
        var payload =
            await response.ReadApiAsync<IReadOnlyList<PlanResponse>>();
        Assert.NotNull(payload?.Data);
        Assert.Contains(payload.Data, p => p.Code == "starter");
        Assert.Contains(payload.Data, p => p.Code == "pro");
    }

    [SkippableFact]
    public async Task Subscribe_creates_tenant_subscription_and_owner_membership()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (userAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using var client = app.Client.WithBearer(userAuth.AccessToken);

        var slug = $"co-{Guid.NewGuid():N}"[..18];
        var subscribeResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest("starter", slug, "Checkout Org", "Tagline", "About us"));
        subscribeResponse.EnsureSuccessStatusCode();
        var subscribed =
            await subscribeResponse.ReadApiAsync<SubscribeToPlanResponse>();
        Assert.NotNull(subscribed?.Data);
        Assert.Equal(slug, subscribed.Data.TenantSlug);
        Assert.Equal("Trialing", subscribed.Data.SubscriptionStatus);

        var publicTenant = await app.Client.GetAsync($"/api/v1/public/tenants/{slug}");
        publicTenant.EnsureSuccessStatusCode();

        var subResponse = await client.GetAsync(
            $"/api/v1/billing/tenants/{subscribed.Data.TenantId}/subscription");
        subResponse.EnsureSuccessStatusCode();
        var sub =
            await subResponse.ReadApiAsync<SubscriptionResponse>();
        Assert.NotNull(sub?.Data);
        Assert.Equal("starter", sub.Data.PlanCode);
        Assert.True(sub.Data.AllowsWriteAccess);

        client.DefaultRequestHeaders.Add(TenantHttpHeaders.TenantId, subscribed.Data.TenantId.ToString());
        var mineResponse = await client.GetAsync("/api/v1/tenants/mine");
        mineResponse.EnsureSuccessStatusCode();
        var mine =
            await mineResponse.ReadApiAsync<IReadOnlyList<TenantSummaryResponse>>();
        Assert.Contains(mine!.Data!, t => t.Id == subscribed.Data.TenantId);
    }

    [SkippableFact]
    public async Task Subscribe_creates_tenant_invoice()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (userAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using var client = app.Client.WithBearer(userAuth.AccessToken);

        var slug = $"inv-{Guid.NewGuid():N}"[..18];
        var subscribeResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest("starter", slug, "Invoice Org", null, null));
        subscribeResponse.EnsureSuccessStatusCode();
        var subscribed =
            await subscribeResponse.ReadApiAsync<SubscribeToPlanResponse>();
        Assert.NotNull(subscribed?.Data);

        var invoicesResponse = await client.GetAsync(
            $"/api/v1/billing/tenants/{subscribed.Data.TenantId}/invoices");
        invoicesResponse.EnsureSuccessStatusCode();
        var invoices =
            await invoicesResponse.ReadApiAsync<IReadOnlyList<BillingInvoiceResponse>>();
        Assert.NotNull(invoices?.Data);
        Assert.NotEmpty(invoices.Data);
        Assert.All(invoices.Data, i => Assert.Equal("Paid", i.Status));
    }

    [SkippableFact]
    public async Task Admin_can_create_custom_plan()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);
        var admin = await app.LoginAdminAsync();
        using var adminClient = app.Client.WithBearer(admin.AccessToken);

        var code = $"team-{Guid.NewGuid():N}"[..12];
        var response = await adminClient.PostAsJsonAsync(
            "/api/v1/platform/plans",
            new CreatePlanRequest(
                code,
                "Team",
                "Custom plan",
                19900,
                "USD",
                7,
                new PlanLimitsRequest(50, 20_000_000_000, ["support", "media"], 200, 5, false, false),
                30));
        response.EnsureSuccessStatusCode();
        var plan = await response.ReadApiAsync<PlanResponse>();
        Assert.Equal(code, plan!.Data!.Code);
    }

    [SkippableFact]
    public async Task Admin_can_update_currency_and_delete_unused_plan()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);
        var admin = await app.LoginAdminAsync();
        using var adminClient = app.Client.WithBearer(admin.AccessToken);

        var code = $"eur-{Guid.NewGuid():N}"[..12];
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/platform/plans",
            new CreatePlanRequest(
                code,
                "Euro Team",
                "EUR plan",
                1500,
                "EUR",
                0,
                new PlanLimitsRequest(10, 1_000_000, ["support"], 5, 1, false, false),
                40));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.ReadApiAsync<PlanResponse>();
        var planId = created!.Data!.Id;

        var updateResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/platform/plans/{planId}",
            new UpdatePlanRequest(
                "Euro Team Plus",
                "Updated",
                1999,
                "VND",
                7,
                new PlanLimitsRequest(20, 2_000_000, ["support", "media"], 10, 2, false, false),
                true,
                41));
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.ReadApiAsync<PlanResponse>();
        Assert.Equal("VND", updated!.Data!.Currency);

        var deleteResponse = await adminClient.DeleteAsync($"/api/v1/platform/plans/{planId}");
        deleteResponse.EnsureSuccessStatusCode();
    }

    [SkippableFact]
    public async Task Tenant_owner_can_invite_member_by_email()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (ownerAuth, _) = await app.RegisterVerifyAndLoginAsync();
        var (inviteeAuth, inviteeEmail) = await app.RegisterVerifyAndLoginAsync();

        using var ownerClient = app.Client.WithBearer(ownerAuth.AccessToken);
        var slug = $"inv-{Guid.NewGuid():N}"[..18];
        var subscribeResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest("starter", slug, "Invite Org", null, null));
        subscribeResponse.EnsureSuccessStatusCode();
        var subscribed =
            await subscribeResponse.ReadApiAsync<SubscribeToPlanResponse>();
        Assert.NotNull(subscribed?.Data);

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            $"/api/v1/tenants/{subscribed.Data.TenantId}/members",
            new InviteTenantMemberRequest(inviteeEmail, "Member"));
        inviteResponse.EnsureSuccessStatusCode();
        var member =
            await inviteResponse.ReadApiAsync<TenantMemberResponse>();
        Assert.NotNull(member?.Data);
        Assert.Equal(inviteeAuth.UserId, member.Data.UserId);
    }

    [SkippableFact]
    public async Task Invite_blocked_when_member_limit_reached()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);
        var admin = await app.LoginAdminAsync();
        using var adminClient = app.Client.WithBearer(admin.AccessToken);

        var planCode = $"solo-{Guid.NewGuid():N}"[..10];
        var planResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/platform/plans",
            new CreatePlanRequest(
                planCode,
                "Solo",
                "One seat only",
                1000,
                "USD",
                0,
                new PlanLimitsRequest(1, 1_000_000, ["support"], 1, 1, false, false),
                99));
        planResponse.EnsureSuccessStatusCode();

        var (ownerAuth, _) = await app.RegisterVerifyAndLoginAsync();
        var (_, inviteeEmail) = await app.RegisterVerifyAndLoginAsync();
        using var ownerClient = app.Client.WithBearer(ownerAuth.AccessToken);

        var slug = $"lim-{Guid.NewGuid():N}"[..18];
        var subscribeResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest(planCode, slug, "Limit Org", null, null));
        subscribeResponse.EnsureSuccessStatusCode();
        var subscribed =
            await subscribeResponse.ReadApiAsync<SubscribeToPlanResponse>();
        Assert.NotNull(subscribed?.Data);

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            $"/api/v1/tenants/{subscribed.Data.TenantId}/members",
            new InviteTenantMemberRequest(inviteeEmail, "Member"));
        Assert.Equal(HttpStatusCode.BadRequest, inviteResponse.StatusCode);
    }

    [SkippableFact]
    public async Task Owner_can_cancel_and_change_subscription_plan()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (ownerAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using var client = app.Client.WithBearer(ownerAuth.AccessToken);

        var slug = $"bp-{Guid.NewGuid():N}"[..18];
        var subscribeResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest("starter", slug, "Billing Portal Org", null, null));
        subscribeResponse.EnsureSuccessStatusCode();
        var subscribed =
            await subscribeResponse.ReadApiAsync<SubscribeToPlanResponse>();
        Assert.NotNull(subscribed?.Data);

        var changeResponse = await client.PutAsJsonAsync(
            $"/api/v1/billing/tenants/{subscribed.Data.TenantId}/subscription/plan",
            new ChangeSubscriptionPlanRequest("pro"));
        changeResponse.EnsureSuccessStatusCode();
        var changed =
            await changeResponse.ReadApiAsync<SubscriptionResponse>();
        Assert.NotNull(changed?.Data);
        Assert.Equal("pro", changed.Data.PlanCode);

        var cancelResponse = await client.PostAsync(
            $"/api/v1/billing/tenants/{subscribed.Data.TenantId}/subscription/cancel",
            null);
        cancelResponse.EnsureSuccessStatusCode();
        var canceled =
            await cancelResponse.ReadApiAsync<SubscriptionResponse>();
        Assert.NotNull(canceled?.Data);
        Assert.Equal("Canceled", canceled.Data.Status);
    }

    [SkippableFact]
    public async Task Pro_subscription_monthly_credit_grant_credits_owner_wallet()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (ownerAuth, _) = await app.RegisterVerifyAndLoginAsync();
        var ownerId = await app.GetCurrentUserIdAsync(ownerAuth.AccessToken);
        using var client = app.Client.WithBearer(ownerAuth.AccessToken);

        var slug = $"cg-{Guid.NewGuid():N}"[..18];
        var subscribeResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest("pro", slug, "Credit Grant Org", null, null));
        subscribeResponse.EnsureSuccessStatusCode();

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var grantResult =
                await sender.Send(new ProcessSubscriptionCreditGrantsCommand());
            Assert.True(grantResult.IsSuccess);
            Assert.True(grantResult.Value >= 1);
        }

        var balanceResponse = await client.GetAsync("/api/v1/wallet/balance");
        balanceResponse.EnsureSuccessStatusCode();
        var balance =
            await balanceResponse.ReadApiAsync<WalletBalanceResponse>();
        Assert.NotNull(balance?.Data);
        Assert.True(balance.Data.BalanceCredits >= 500);
        _ = ownerId;
    }
}