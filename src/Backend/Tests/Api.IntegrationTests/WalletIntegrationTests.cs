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
}
