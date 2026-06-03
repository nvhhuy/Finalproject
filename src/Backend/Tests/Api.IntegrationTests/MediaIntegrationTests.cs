using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models;
using Api.Tenancy;
using Application.Contracts.Storage;
using Application.Modules.Media.Commands;
using Application.Modules.Media.Dtos;
using Domains.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class MediaIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public MediaIntegrationTests(IntegrationAppFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Media_initiate_complete_and_get_asset_when_object_storage_is_available()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        Skip.IfNot(
            string.Equals(Environment.GetEnvironmentVariable("INTEGRATION_MEDIA_ENABLED"), "true",
                StringComparison.OrdinalIgnoreCase),
            "Set INTEGRATION_MEDIA_ENABLED=true and start MinIO (local/docker-compose.yml) to run media E2E.");

        var app = IntegrationTestApp.FromFixture(_fixture);
        var (auth, _) = await app.RegisterVerifyAndLoginAsync();
        using var userClient = app.Client.WithBearer(auth.AccessToken);
        userClient.DefaultRequestHeaders.Add(TenantHttpHeaders.TenantId, SystemTenants.DefaultTenantId.ToString());

        var initiateResponse = await userClient.PostAsJsonAsync(
            "/api/v1/media/uploads",
            new { contentType = "image/png", byteSize = 68, fileExtension = "png" });

        if (!initiateResponse.IsSuccessStatusCode)
        {
            Skip.If(true, "Object storage is not reachable; start MinIO and retry.");
            return;
        }

        var initiated =
            await initiateResponse.Content.ReadFromJsonAsync<ApiResponse<InitiateMediaUploadResult>>();
        Assert.NotNull(initiated?.Data);

        byte[] pngBytes =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
            0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
            0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
            0x42, 0x60, 0x82
        ];

        using HttpClient storage = new() { Timeout = TimeSpan.FromSeconds(30) };
        using var put = new HttpRequestMessage(HttpMethod.Put, initiated.Data!.UploadUrl)
        {
            Content = new ByteArrayContent(pngBytes)
        };
        put.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var putResponse = await storage.SendAsync(put);
        if (!putResponse.IsSuccessStatusCode)
        {
            var putBody = await putResponse.Content.ReadAsStringAsync();
            Skip.If(true,
                $"Presigned PUT failed ({putResponse.StatusCode}): {putBody}; check MinIO configuration.");
            return;
        }

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var objectStorage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
            var exists = await objectStorage.ExistsAsync(initiated.Data.StorageKey, CancellationToken.None);
            Assert.True(
                exists,
                $"Uploaded object not visible at storage key '{initiated.Data.StorageKey}' before complete.");
        }

        using var complete =
            new HttpRequestMessage(HttpMethod.Post, $"/api/v1/media/{initiated.Data.MediaAssetId}/complete")
                .WithIdempotencyKey();
        var completeResponse = await userClient.SendAsync(complete);
        if (!completeResponse.IsSuccessStatusCode)
        {
            var completeBody = await completeResponse.Content.ReadAsStringAsync();
            Assert.Fail($"Complete media upload failed ({completeResponse.StatusCode}): {completeBody}");
        }

        var getResponse = await userClient.GetAsync($"/api/v1/media/{initiated.Data.MediaAssetId}");
        var asset = await getResponse.ReadApiAsync<MediaAssetDto>();
        Assert.NotNull(asset?.Data);
        Assert.Equal(initiated.Data.MediaAssetId, asset.Data.Id);
    }
}