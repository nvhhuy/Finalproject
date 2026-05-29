using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models;
using Api.Models.Auth;
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

    public BillingIntegrationTests(IntegrationAppFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task Public_plans_lists_seeded_catalog()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        HttpResponseMessage response = await app.Client.GetAsync("/api/v1/public/plans");
        response.EnsureSuccessStatusCode();
        ApiResponse<IReadOnlyList<PlanResponse>>? payload =
            await response.ReadApiAsync<IReadOnlyList<PlanResponse>>();
        Assert.NotNull(payload?.Data);
        Assert.Contains(payload.Data, p => p.Code == "starter");
        Assert.Contains(payload.Data, p => p.Code == "pro");
    }

    [SkippableFact]
    public async Task Subscribe_creates_tenant_subscription_and_owner_membership()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        (AuthResponseDto userAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using HttpClient client = app.Client.WithBearer(userAuth.AccessToken);

        string slug = $"co-{Guid.NewGuid():N}"[..18];
        HttpResponseMessage subscribeResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest("starter", slug, "Checkout Org", "Tagline", "About us"));
        subscribeResponse.EnsureSuccessStatusCode();
        ApiResponse<SubscribeToPlanResponse>? subscribed =
            await subscribeResponse.ReadApiAsync<SubscribeToPlanResponse>();
        Assert.NotNull(subscribed?.Data);
        Assert.Equal(slug, subscribed.Data.TenantSlug);
        Assert.Equal("Trialing", subscribed.Data.SubscriptionStatus);

        HttpResponseMessage publicTenant = await app.Client.GetAsync($"/api/v1/public/tenants/{slug}");
        publicTenant.EnsureSuccessStatusCode();

        HttpResponseMessage subResponse = await client.GetAsync(
            $"/api/v1/billing/tenants/{subscribed.Data.TenantId}/subscription");
        subResponse.EnsureSuccessStatusCode();
        ApiResponse<SubscriptionResponse>? sub =
            await subResponse.ReadApiAsync<SubscriptionResponse>();
        Assert.NotNull(sub?.Data);
        Assert.Equal("starter", sub.Data.PlanCode);
        Assert.True(sub.Data.AllowsWriteAccess);

        client.DefaultRequestHeaders.Add(TenantHttpHeaders.TenantId, subscribed.Data.TenantId.ToString());
        HttpResponseMessage mineResponse = await client.GetAsync("/api/v1/tenants/mine");
        mineResponse.EnsureSuccessStatusCode();
        ApiResponse<IReadOnlyList<TenantSummaryResponse>>? mine =
            await mineResponse.ReadApiAsync<IReadOnlyList<TenantSummaryResponse>>();
        Assert.Contains(mine!.Data!, t => t.Id == subscribed.Data.TenantId);
    }

    [SkippableFact]
    public async Task Subscribe_creates_tenant_invoice()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        (AuthResponseDto userAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using HttpClient client = app.Client.WithBearer(userAuth.AccessToken);

        string slug = $"inv-{Guid.NewGuid():N}"[..18];
        HttpResponseMessage subscribeResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest("starter", slug, "Invoice Org", null, null));
        subscribeResponse.EnsureSuccessStatusCode();
        ApiResponse<SubscribeToPlanResponse>? subscribed =
            await subscribeResponse.ReadApiAsync<SubscribeToPlanResponse>();
        Assert.NotNull(subscribed?.Data);

        HttpResponseMessage invoicesResponse = await client.GetAsync(
            $"/api/v1/billing/tenants/{subscribed.Data.TenantId}/invoices");
        invoicesResponse.EnsureSuccessStatusCode();
        ApiResponse<IReadOnlyList<BillingInvoiceResponse>>? invoices =
            await invoicesResponse.ReadApiAsync<IReadOnlyList<BillingInvoiceResponse>>();
        Assert.NotNull(invoices?.Data);
        Assert.NotEmpty(invoices.Data);
        Assert.All(invoices.Data, i => Assert.Equal("Paid", i.Status));
    }

    [SkippableFact]
    public async Task Admin_can_create_custom_plan()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);
        AuthResponseDto admin = await app.LoginAdminAsync();
        using HttpClient adminClient = app.Client.WithBearer(admin.AccessToken);

        string code = $"team-{Guid.NewGuid():N}"[..12];
        HttpResponseMessage response = await adminClient.PostAsJsonAsync(
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
        ApiResponse<PlanResponse>? plan = await response.ReadApiAsync<PlanResponse>();
        Assert.Equal(code, plan!.Data!.Code);
    }

    [SkippableFact]
    public async Task Admin_can_update_currency_and_delete_unused_plan()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);
        AuthResponseDto admin = await app.LoginAdminAsync();
        using HttpClient adminClient = app.Client.WithBearer(admin.AccessToken);

        string code = $"eur-{Guid.NewGuid():N}"[..12];
        HttpResponseMessage createResponse = await adminClient.PostAsJsonAsync(
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
        ApiResponse<PlanResponse>? created = await createResponse.ReadApiAsync<PlanResponse>();
        Guid planId = created!.Data!.Id;

        HttpResponseMessage updateResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/platform/plans/{planId}",
            new UpdatePlanRequest(
                "Euro Team Plus",
                "Updated",
                1999,
                "VND",
                7,
                new PlanLimitsRequest(20, 2_000_000, ["support", "media"], 10, 2, false, false),
                IsActive: true,
                SortOrder: 41));
        updateResponse.EnsureSuccessStatusCode();
        ApiResponse<PlanResponse>? updated = await updateResponse.ReadApiAsync<PlanResponse>();
        Assert.Equal("VND", updated!.Data!.Currency);

        HttpResponseMessage deleteResponse = await adminClient.DeleteAsync($"/api/v1/platform/plans/{planId}");
        deleteResponse.EnsureSuccessStatusCode();
    }

    [SkippableFact]
    public async Task Tenant_owner_can_invite_member_by_email()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        (AuthResponseDto ownerAuth, _) = await app.RegisterVerifyAndLoginAsync();
        (AuthResponseDto inviteeAuth, string inviteeEmail) = await app.RegisterVerifyAndLoginAsync();

        using HttpClient ownerClient = app.Client.WithBearer(ownerAuth.AccessToken);
        string slug = $"inv-{Guid.NewGuid():N}"[..18];
        HttpResponseMessage subscribeResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest("starter", slug, "Invite Org", null, null));
        subscribeResponse.EnsureSuccessStatusCode();
        ApiResponse<SubscribeToPlanResponse>? subscribed =
            await subscribeResponse.ReadApiAsync<SubscribeToPlanResponse>();
        Assert.NotNull(subscribed?.Data);

        HttpResponseMessage inviteResponse = await ownerClient.PostAsJsonAsync(
            $"/api/v1/tenants/{subscribed.Data.TenantId}/members",
            new InviteTenantMemberRequest(inviteeEmail, "Member"));
        inviteResponse.EnsureSuccessStatusCode();
        ApiResponse<TenantMemberResponse>? member =
            await inviteResponse.ReadApiAsync<TenantMemberResponse>();
        Assert.NotNull(member?.Data);
        Assert.Equal(inviteeAuth.UserId, member.Data.UserId);
    }

    [SkippableFact]
    public async Task Invite_blocked_when_member_limit_reached()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);
        AuthResponseDto admin = await app.LoginAdminAsync();
        using HttpClient adminClient = app.Client.WithBearer(admin.AccessToken);

        string planCode = $"solo-{Guid.NewGuid():N}"[..10];
        HttpResponseMessage planResponse = await adminClient.PostAsJsonAsync(
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

        (AuthResponseDto ownerAuth, _) = await app.RegisterVerifyAndLoginAsync();
        (_, string inviteeEmail) = await app.RegisterVerifyAndLoginAsync();
        using HttpClient ownerClient = app.Client.WithBearer(ownerAuth.AccessToken);

        string slug = $"lim-{Guid.NewGuid():N}"[..18];
        HttpResponseMessage subscribeResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest(planCode, slug, "Limit Org", null, null));
        subscribeResponse.EnsureSuccessStatusCode();
        ApiResponse<SubscribeToPlanResponse>? subscribed =
            await subscribeResponse.ReadApiAsync<SubscribeToPlanResponse>();
        Assert.NotNull(subscribed?.Data);

        HttpResponseMessage inviteResponse = await ownerClient.PostAsJsonAsync(
            $"/api/v1/tenants/{subscribed.Data.TenantId}/members",
            new InviteTenantMemberRequest(inviteeEmail, "Member"));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, inviteResponse.StatusCode);
    }

    [SkippableFact]
    public async Task Owner_can_cancel_and_change_subscription_plan()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        (AuthResponseDto ownerAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using HttpClient client = app.Client.WithBearer(ownerAuth.AccessToken);

        string slug = $"bp-{Guid.NewGuid():N}"[..18];
        HttpResponseMessage subscribeResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest("starter", slug, "Billing Portal Org", null, null));
        subscribeResponse.EnsureSuccessStatusCode();
        ApiResponse<SubscribeToPlanResponse>? subscribed =
            await subscribeResponse.ReadApiAsync<SubscribeToPlanResponse>();
        Assert.NotNull(subscribed?.Data);

        HttpResponseMessage changeResponse = await client.PutAsJsonAsync(
            $"/api/v1/billing/tenants/{subscribed.Data.TenantId}/subscription/plan",
            new ChangeSubscriptionPlanRequest("pro"));
        changeResponse.EnsureSuccessStatusCode();
        ApiResponse<SubscriptionResponse>? changed =
            await changeResponse.ReadApiAsync<SubscriptionResponse>();
        Assert.NotNull(changed?.Data);
        Assert.Equal("pro", changed.Data.PlanCode);

        HttpResponseMessage cancelResponse = await client.PostAsync(
            $"/api/v1/billing/tenants/{subscribed.Data.TenantId}/subscription/cancel",
            null);
        cancelResponse.EnsureSuccessStatusCode();
        ApiResponse<SubscriptionResponse>? canceled =
            await cancelResponse.ReadApiAsync<SubscriptionResponse>();
        Assert.NotNull(canceled?.Data);
        Assert.Equal("Canceled", canceled.Data.Status);
    }

    [SkippableFact]
    public async Task Pro_subscription_monthly_credit_grant_credits_owner_wallet()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        (AuthResponseDto ownerAuth, _) = await app.RegisterVerifyAndLoginAsync();
        Guid ownerId = await app.GetCurrentUserIdAsync(ownerAuth.AccessToken);
        using HttpClient client = app.Client.WithBearer(ownerAuth.AccessToken);

        string slug = $"cg-{Guid.NewGuid():N}"[..18];
        HttpResponseMessage subscribeResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/subscribe",
            new SubscribeToPlanRequest("pro", slug, "Credit Grant Org", null, null));
        subscribeResponse.EnsureSuccessStatusCode();

        using (IServiceScope scope = _fixture.Factory.Services.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Shared.Common.Result<int, Shared.Errors.Error> grantResult =
                await sender.Send(new ProcessSubscriptionCreditGrantsCommand());
            Assert.True(grantResult.IsSuccess);
            Assert.True(grantResult.Value >= 1);
        }

        HttpResponseMessage balanceResponse = await client.GetAsync("/api/v1/wallet/balance");
        balanceResponse.EnsureSuccessStatusCode();
        ApiResponse<WalletBalanceResponse>? balance =
            await balanceResponse.ReadApiAsync<WalletBalanceResponse>();
        Assert.NotNull(balance?.Data);
        Assert.True(balance.Data.BalanceCredits >= 500);
        _ = ownerId;
    }
}
