using System.Net.Http.Json;
using System.Text;
using Api.IntegrationTests.Support;
using Api.Models;
using Api.Models.Auth;
using Api.Models.Billing;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class PaymentWebhookIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public PaymentWebhookIntegrationTests(IntegrationAppFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task Stripe_webhook_is_idempotent_by_event_id()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        string eventId = $"evt_{Guid.NewGuid():N}";
        string payload = $$"""
            {
              "id": "{{eventId}}",
              "type": "checkout.session.completed",
              "data": { "object": { "id": "cs_test" } }
            }
            """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        HttpResponseMessage first = await app.Client.PostAsync("/api/v1/webhooks/stripe", content);
        first.EnsureSuccessStatusCode();
        ApiResponse<PaymentWebhookReceiptResponse>? firstBody =
            await first.ReadApiAsync<PaymentWebhookReceiptResponse>();
        Assert.NotNull(firstBody?.Data);
        Assert.False(firstBody.Data.Duplicate);

        using var content2 = new StringContent(payload, Encoding.UTF8, "application/json");
        HttpResponseMessage second = await app.Client.PostAsync("/api/v1/webhooks/stripe", content2);
        second.EnsureSuccessStatusCode();
        ApiResponse<PaymentWebhookReceiptResponse>? secondBody =
            await second.ReadApiAsync<PaymentWebhookReceiptResponse>();
        Assert.NotNull(secondBody?.Data);
        Assert.True(secondBody.Data.Duplicate);
        Assert.Equal(firstBody.Data.EventId, secondBody.Data.EventId);
    }

    [SkippableFact]
    public async Task Stripe_checkout_completed_with_metadata_provisions_tenant()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        (AuthResponseDto userAuth, _) = await app.RegisterVerifyAndLoginAsync();
        Guid userId = await app.GetCurrentUserIdAsync(userAuth.AccessToken);

        string slug = $"wh-{Guid.NewGuid():N}"[..18];
        string eventId = $"evt_{Guid.NewGuid():N}";
        string payload = $$"""
            {
              "id": "{{eventId}}",
              "type": "checkout.session.completed",
              "data": {
                "object": {
                  "metadata": {
                    "user_id": "{{userId}}",
                    "plan_code": "starter",
                    "tenant_slug": "{{slug}}",
                    "tenant_display_name": "Webhook Org"
                  }
                }
              }
            }
            """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        HttpResponseMessage response = await app.Client.PostAsync("/api/v1/webhooks/stripe", content);
        response.EnsureSuccessStatusCode();
        ApiResponse<PaymentWebhookReceiptResponse>? body =
            await response.ReadApiAsync<PaymentWebhookReceiptResponse>();
        Assert.NotNull(body?.Data);
        Assert.Equal("Processed", body.Data.Status);

        HttpResponseMessage publicTenant = await app.Client.GetAsync($"/api/v1/public/tenants/{slug}");
        publicTenant.EnsureSuccessStatusCode();
    }

    [SkippableTheory]
    [InlineData("vnpay")]
    [InlineData("payos")]
    public async Task Regional_webhook_is_idempotent_by_event_id(string provider)
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        string eventId = $"ord_{Guid.NewGuid():N}";
        string payload = $$"""
            {
              "id": "{{eventId}}",
              "type": "payment.success",
              "amount": 1000
            }
            """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        HttpResponseMessage first = await app.Client.PostAsync($"/api/v1/webhooks/{provider}", content);
        first.EnsureSuccessStatusCode();
        ApiResponse<PaymentWebhookReceiptResponse>? firstBody =
            await first.ReadApiAsync<PaymentWebhookReceiptResponse>();
        Assert.NotNull(firstBody?.Data);
        Assert.False(firstBody.Data.Duplicate);

        using var content2 = new StringContent(payload, Encoding.UTF8, "application/json");
        HttpResponseMessage second = await app.Client.PostAsync($"/api/v1/webhooks/{provider}", content2);
        second.EnsureSuccessStatusCode();
        ApiResponse<PaymentWebhookReceiptResponse>? secondBody =
            await second.ReadApiAsync<PaymentWebhookReceiptResponse>();
        Assert.NotNull(secondBody?.Data);
        Assert.True(secondBody.Data.Duplicate);
    }
}
