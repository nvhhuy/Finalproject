using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models;
using Api.Models.Auth;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class SupportTicketsIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public SupportTicketsIntegrationTests(IntegrationAppFixture fixture) => _fixture = fixture;

    private sealed record SupportTicketListDto(IReadOnlyList<SupportTicketSummaryDto> Items, int TotalCount, int Page, int PageSize);
    private sealed record SupportTicketSummaryDto(Guid Id, string TicketNumber, string Subject, string Priority, string Status);
    private sealed record SupportTicketDetailDto(Guid Id, string TicketNumber, string Subject, string Status, IReadOnlyList<SupportTicketMessageDto> Messages);
    private sealed record SupportTicketMessageDto(Guid Id, string Body, bool IsInternalNote);

    [SkippableFact]
    public async Task User_and_admin_can_run_support_ticket_lifecycle()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        (AuthResponseDto userAuth, _) = await app.RegisterVerifyAndLoginAsync();
        AuthResponseDto adminAuth = await app.LoginAdminAsync();

        using HttpClient userClient = app.Client.WithBearer(userAuth.AccessToken);
        using HttpClient adminClient = _fixture.Factory.CreateClient().WithBearer(adminAuth.AccessToken);

        using HttpRequestMessage create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/support/tickets")
        {
            Content = JsonContent.Create(new
            {
                subject = "Cannot update profile from mobile",
                description = "Saving profile fails after submit button click.",
                category = "Profile",
                priority = "High",
            }),
        }.WithIdempotencyKey();

        HttpResponseMessage createResponse = await userClient.SendAsync(create);
        ApiResponse<Guid>? created = await createResponse.ReadApiAsync<Guid>();
        Assert.NotNull(created?.Data);
        Guid ticketId = created.Data;

        HttpResponseMessage userListResponse = await userClient.GetAsync("/api/v1/support/tickets?page=1&pageSize=20");
        ApiResponse<SupportTicketListDto>? userList = await userListResponse.ReadApiAsync<SupportTicketListDto>();
        Assert.NotNull(userList?.Data);
        Assert.Contains(userList.Data.Items, i => i.Id == ticketId);

        HttpResponseMessage adminListResponse = await adminClient.GetAsync("/api/v1/admin/support/tickets?page=1&pageSize=20&search=mobile");
        ApiResponse<SupportTicketListDto>? adminList = await adminListResponse.ReadApiAsync<SupportTicketListDto>();
        Assert.NotNull(adminList?.Data);
        Assert.Contains(adminList.Data.Items, i => i.Id == ticketId);

        using HttpRequestMessage adminReply = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/support/tickets/{ticketId}/messages")
        {
            Content = JsonContent.Create(new
            {
                body = "We reproduced it and shipped a fix. Please try again.",
                isInternalNote = false,
            }),
        }.WithIdempotencyKey();
        (await adminClient.SendAsync(adminReply)).EnsureSuccessStatusCode();

        HttpResponseMessage statusResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/support/tickets/{ticketId}/status",
            new { status = "Resolved" });
        statusResponse.EnsureSuccessStatusCode();

        HttpResponseMessage userDetailResponse = await userClient.GetAsync($"/api/v1/support/tickets/{ticketId}");
        ApiResponse<SupportTicketDetailDto>? userDetail = await userDetailResponse.ReadApiAsync<SupportTicketDetailDto>();
        Assert.NotNull(userDetail?.Data);
        Assert.Equal("Resolved", userDetail.Data.Status);
        Assert.Contains(userDetail.Data.Messages, m => m.Body.Contains("shipped a fix", StringComparison.OrdinalIgnoreCase));
    }
}
