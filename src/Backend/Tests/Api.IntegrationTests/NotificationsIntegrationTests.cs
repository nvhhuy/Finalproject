using System.Net;
using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models.Admin;
using Application.Modules.Notifications.Queries;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class NotificationsIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public NotificationsIntegrationTests(IntegrationAppFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Admin_broadcast_user_lists_and_marks_notification_read()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (userAuth, _) = await app.RegisterVerifyAndLoginAsync();
        var adminAuth = await app.LoginAdminAsync();

        using var adminClient = app.Client.WithBearer(adminAuth.AccessToken);
        using var broadcast = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/notifications/broadcast")
        {
            Content = JsonContent.Create(new BroadcastMemberNotificationRequest(
                "Platform maintenance",
                "Scheduled maintenance completes tonight.",
                "/app"))
        }.WithIdempotencyKey();

        var broadcastResponse = await adminClient.SendAsync(broadcast);
        broadcastResponse.EnsureSuccessStatusCode();
        var broadcastPayload =
            await broadcastResponse.ReadApiAsync<BroadcastMemberNotificationResponse>();
        Assert.NotNull(broadcastPayload?.Data);
        Assert.True(broadcastPayload.Data.RecipientCount >= 1);

        using var userClient = app.Client.WithBearer(userAuth.AccessToken);
        var listResponse = await userClient.GetAsync("/api/v1/notifications?page=1&pageSize=20");
        var list =
            await listResponse.ReadApiAsync<IReadOnlyList<NotificationDto>>();
        Assert.NotNull(list?.Data);
        var notification = Assert.Single(
            list.Data,
            n => n.Title == "Platform maintenance" &&
                 n.Body.Contains("Scheduled maintenance", StringComparison.Ordinal));
        Assert.Null(notification.ReadAtUtc);

        var markReadResponse =
            await userClient.PostAsync($"/api/v1/notifications/{notification.Id}/read", null);
        markReadResponse.EnsureSuccessStatusCode();

        var listAfterRead = await userClient.GetAsync("/api/v1/notifications?page=1&pageSize=20");
        var listAfter =
            await listAfterRead.ReadApiAsync<IReadOnlyList<NotificationDto>>();
        var readRow = listAfter?.Data?.FirstOrDefault(n => n.Id == notification.Id);
        Assert.NotNull(readRow);
        Assert.NotNull(readRow.ReadAtUtc);
    }

    [SkippableFact]
    public async Task Mark_read_returns_not_found_for_unknown_notification()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (userAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using var userClient = app.Client.WithBearer(userAuth.AccessToken);

        var response =
            await userClient.PostAsync($"/api/v1/notifications/{Guid.NewGuid()}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}