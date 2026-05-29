using System.Text;
using Api.IntegrationTests.Support;
using Api.Models.Billing;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class PaymentWebhookIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public PaymentWebhookIntegrationTests(IntegrationAppFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Stripe_webhook_is_idempotent_by_event_id()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var eventId = $"evt_{Guid.NewGuid():N}";
        var payload = $$"""
                        {
                          "id": "{{eventId}}",
                          "type": "checkout.session.completed",
                          "data": { "object": { "id": "cs_test" } }
                        }
                        """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var first = await app.Client.PostAsync("/api/v1/webhooks/stripe", content);
        first.EnsureSuccessStatusCode();
        var firstBody =
            await first.ReadApiAsync<PaymentWebhookReceiptResponse>();
        Assert.NotNull(firstBody?.Data);
        Assert.False(firstBody.Data.Duplicate);

        using var content2 = new StringContent(payload, Encoding.UTF8, "application/json");
        var second = await app.Client.PostAsync("/api/v1/webhooks/stripe", content2);
        second.EnsureSuccessStatusCode();
        var secondBody =
            await second.ReadApiAsync<PaymentWebhookReceiptResponse>();
        Assert.NotNull(secondBody?.Data);
        Assert.True(secondBody.Data.Duplicate);
        Assert.Equal(firstBody.Data.EventId, secondBody.Data.EventId);
    }

    [SkippableFact]
    public async Task Stripe_checkout_completed_with_metadata_provisions_tenant()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (userAuth, _) = await app.RegisterVerifyAndLoginAsync();
        var userId = await app.GetCurrentUserIdAsync(userAuth.AccessToken);

        var slug = $"wh-{Guid.NewGuid():N}"[..18];
        var eventId = $"evt_{Guid.NewGuid():N}";
        var payload = $$"""
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
        var response = await app.Client.PostAsync("/api/v1/webhooks/stripe", content);
        response.EnsureSuccessStatusCode();
        var body =
            await response.ReadApiAsync<PaymentWebhookReceiptResponse>();
        Assert.NotNull(body?.Data);
        Assert.Equal("Processed", body.Data.Status);

        var publicTenant = await app.Client.GetAsync($"/api/v1/public/tenants/{slug}");
        publicTenant.EnsureSuccessStatusCode();
    }

    [SkippableTheory]
    [InlineData("vnpay")]
    [InlineData("payos")]
    public async Task Regional_webhook_is_idempotent_by_event_id(string provider)
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var eventId = $"ord_{Guid.NewGuid():N}";
        var payload = $$"""
                        {
                          "id": "{{eventId}}",
                          "type": "payment.success",
                          "amount": 1000
                        }
                        """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var first = await app.Client.PostAsync($"/api/v1/webhooks/{provider}", content);
        first.EnsureSuccessStatusCode();
        var firstBody =
            await first.ReadApiAsync<PaymentWebhookReceiptResponse>();
        Assert.NotNull(firstBody?.Data);
        Assert.False(firstBody.Data.Duplicate);

        using var content2 = new StringContent(payload, Encoding.UTF8, "application/json");
        var second = await app.Client.PostAsync($"/api/v1/webhooks/{provider}", content2);
        second.EnsureSuccessStatusCode();
        var secondBody =
            await second.ReadApiAsync<PaymentWebhookReceiptResponse>();
        Assert.NotNull(secondBody?.Data);
        Assert.True(secondBody.Data.Duplicate);
    }
}