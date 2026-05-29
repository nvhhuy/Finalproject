using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models;
using Api.Models.Auth;
using Api.Models.Wallet;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class WalletIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public WalletIntegrationTests(IntegrationAppFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task Top_up_and_debit_metered_action()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        (AuthResponseDto userAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using HttpClient client = app.Client.WithBearer(userAuth.AccessToken);

        using var topUpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/top-up");
        topUpRequest.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString("N"));
        topUpRequest.Content = JsonContent.Create(new TopUpWalletRequest(500));
        HttpResponseMessage topUpResponse = await client.SendAsync(topUpRequest);
        if (!topUpResponse.IsSuccessStatusCode)
            throw new HttpRequestException(await topUpResponse.Content.ReadAsStringAsync(), null, topUpResponse.StatusCode);
        topUpResponse.EnsureSuccessStatusCode();
        ApiResponse<WalletBalanceResponse>? balance =
            await topUpResponse.ReadApiAsync<WalletBalanceResponse>();
        Assert.NotNull(balance?.Data);
        Assert.Equal(500, balance.Data.BalanceCredits);

        using var debitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/debit");
        debitRequest.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString("N"));
        debitRequest.Content = JsonContent.Create(new DebitWalletActionRequest("export_report"));
        HttpResponseMessage debitResponse = await client.SendAsync(debitRequest);
        debitResponse.EnsureSuccessStatusCode();
        ApiResponse<WalletDebitResponse>? debit =
            await debitResponse.ReadApiAsync<WalletDebitResponse>();
        Assert.NotNull(debit?.Data);
        Assert.True(debit.Data.Success);
        Assert.Equal(450, debit.Data.RemainingBalance);
    }

    [SkippableFact]
    public async Task Top_up_with_same_idempotency_key_does_not_double_credit()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        (AuthResponseDto userAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using HttpClient client = app.Client.WithBearer(userAuth.AccessToken);
        string idempotencyKey = Guid.NewGuid().ToString("N");

        using var first = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/top-up");
        first.Headers.Add("Idempotency-Key", idempotencyKey);
        first.Content = JsonContent.Create(new TopUpWalletRequest(500));
        (await client.SendAsync(first)).EnsureSuccessStatusCode();

        using var replay = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/top-up");
        replay.Headers.Add("Idempotency-Key", idempotencyKey);
        replay.Content = JsonContent.Create(new TopUpWalletRequest(500));
        HttpResponseMessage replayResponse = await client.SendAsync(replay);
        replayResponse.EnsureSuccessStatusCode();
        ApiResponse<WalletBalanceResponse>? replayBalance =
            await replayResponse.ReadApiAsync<WalletBalanceResponse>();
        Assert.NotNull(replayBalance?.Data);
        Assert.Equal(500, replayBalance.Data.BalanceCredits);
    }

    [SkippableFact]
    public async Task Purchase_credit_package_increases_balance()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        (AuthResponseDto userAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using HttpClient client = app.Client.WithBearer(userAuth.AccessToken);

        using var purchaseRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/purchase");
        purchaseRequest.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString("N"));
        purchaseRequest.Content = JsonContent.Create(new PurchaseCreditPackageRequest("starter-500"));
        HttpResponseMessage purchaseResponse = await client.SendAsync(purchaseRequest);
        purchaseResponse.EnsureSuccessStatusCode();
        ApiResponse<WalletBalanceResponse>? balance =
            await purchaseResponse.ReadApiAsync<WalletBalanceResponse>();
        Assert.NotNull(balance?.Data);
        Assert.Equal(500, balance.Data.BalanceCredits);
    }
}
