using System.Net;
using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models;
using Api.Models.Admin;
using Api.Models.Auth;
using Application.Modules.Notifications.Queries;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class NotificationsIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public NotificationsIntegrationTests(IntegrationAppFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task Admin_broadcast_user_lists_and_marks_notification_read()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        (AuthResponseDto userAuth, _) = await app.RegisterVerifyAndLoginAsync();
        AuthResponseDto adminAuth = await app.LoginAdminAsync();

        using HttpClient adminClient = app.Client.WithBearer(adminAuth.AccessToken);
        using HttpRequestMessage broadcast = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/notifications/broadcast")
        {
            Content = JsonContent.Create(new BroadcastMemberNotificationRequest(
                "Platform maintenance",
                "Scheduled maintenance completes tonight.",
                "/app")),
        }.WithIdempotencyKey();

        HttpResponseMessage broadcastResponse = await adminClient.SendAsync(broadcast);
        broadcastResponse.EnsureSuccessStatusCode();
        ApiResponse<BroadcastMemberNotificationResponse>? broadcastPayload =
            await broadcastResponse.ReadApiAsync<BroadcastMemberNotificationResponse>();
        Assert.NotNull(broadcastPayload?.Data);
        Assert.True(broadcastPayload.Data.RecipientCount >= 1);

        using HttpClient userClient = app.Client.WithBearer(userAuth.AccessToken);
        HttpResponseMessage listResponse = await userClient.GetAsync("/api/v1/notifications?page=1&pageSize=20");
        ApiResponse<IReadOnlyList<NotificationDto>>? list =
            await listResponse.ReadApiAsync<IReadOnlyList<NotificationDto>>();
        Assert.NotNull(list?.Data);
        NotificationDto notification = Assert.Single(
            list.Data,
            n => n.Title == "Platform maintenance" && n.Body.Contains("Scheduled maintenance", StringComparison.Ordinal));
        Assert.Null(notification.ReadAtUtc);

        HttpResponseMessage markReadResponse =
            await userClient.PostAsync($"/api/v1/notifications/{notification.Id}/read", content: null);
        markReadResponse.EnsureSuccessStatusCode();

        HttpResponseMessage listAfterRead = await userClient.GetAsync("/api/v1/notifications?page=1&pageSize=20");
        ApiResponse<IReadOnlyList<NotificationDto>>? listAfter =
            await listAfterRead.ReadApiAsync<IReadOnlyList<NotificationDto>>();
        NotificationDto? readRow = listAfter?.Data?.FirstOrDefault(n => n.Id == notification.Id);
        Assert.NotNull(readRow);
        Assert.NotNull(readRow.ReadAtUtc);
    }

    [SkippableFact]
    public async Task Mark_read_returns_not_found_for_unknown_notification()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        (AuthResponseDto userAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using HttpClient userClient = app.Client.WithBearer(userAuth.AccessToken);

        HttpResponseMessage response =
            await userClient.PostAsync($"/api/v1/notifications/{Guid.NewGuid()}/read", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
