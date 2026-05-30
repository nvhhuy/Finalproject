using System.Net;
using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class ContactIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public ContactIntegrationTests(IntegrationAppFixture fixture)
    {
        _fixture = fixture;
    }

    // -----------------------------------------------------------------------
    // Public submit endpoint
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Public_submit_returns_200_and_a_guid_without_authentication()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);
        app.Client.ClearBearer();

        var response = await app.Client.PostAsJsonAsync("/api/v1/contact", new
        {
            name = "Jane Smith",
            email = "jane@test.local",
            subject = "Integration test subject",
            body = "This message was submitted by an API integration test."
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
        Assert.NotNull(payload);
        Assert.True(payload.Success);
        Assert.NotEqual(Guid.Empty, payload.Data);
    }

    [SkippableFact]
    public async Task Public_submit_with_missing_name_returns_400()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);
        app.Client.ClearBearer();

        var response = await app.Client.PostAsJsonAsync("/api/v1/contact", new
        {
            name = "",
            email = "jane@test.local",
            subject = "Missing name test",
            body = "Body text"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Public_submit_with_missing_email_returns_400()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);
        app.Client.ClearBearer();

        var response = await app.Client.PostAsJsonAsync("/api/v1/contact", new
        {
            name = "Jane Smith",
            email = "",
            subject = "Missing email test",
            body = "Body text"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Public_submit_with_missing_body_returns_400()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);
        app.Client.ClearBearer();

        var response = await app.Client.PostAsJsonAsync("/api/v1/contact", new
        {
            name = "Jane Smith",
            email = "jane@test.local",
            subject = "Missing body test",
            body = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Admin list endpoint
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Admin_list_returns_401_without_authentication()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var client = _fixture.Factory.CreateClient().ClearBearer();

        var response = await client.GetAsync("/api/v1/admin/contact-messages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_list_returns_submitted_message()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        // Submit a message as a public user (no auth)
        var uniqueEmail = $"contact-{Guid.NewGuid():N}@test.local";
        var submitResponse = await app.Client.ClearBearer().PostAsJsonAsync("/api/v1/contact", new
        {
            name = "Integration Tester",
            email = uniqueEmail,
            subject = "List visibility test",
            body = "Checking that admin can see this submission."
        });
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
        Assert.NotNull(submitted?.Data);
        var messageId = submitted.Data;

        // Admin fetches the list and finds it by email
        var adminAuth = await app.LoginAdminAsync();
        using var adminClient = _fixture.Factory.CreateClient().WithBearer(adminAuth.AccessToken);

        var listResponse = await adminClient.GetAsync(
            $"/api/v1/admin/contact-messages?page=1&pageSize=50&search={Uri.EscapeDataString(uniqueEmail)}");
        listResponse.EnsureSuccessStatusCode();

        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<ContactMessageListDto>>();
        Assert.NotNull(list?.Data);
        Assert.Contains(list.Data.Items, m => m.Id == messageId && m.Email == uniqueEmail && !m.IsRead);
    }

    // -----------------------------------------------------------------------
    // Admin mark-read endpoint
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Admin_mark_read_changes_IsRead_flag()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        // Submit a contact message
        var uniqueEmail = $"markread-{Guid.NewGuid():N}@test.local";
        var submitResponse = await app.Client.ClearBearer().PostAsJsonAsync("/api/v1/contact", new
        {
            name = "Mark Read Tester",
            email = uniqueEmail,
            subject = "Mark-read integration test",
            body = "Please mark me as read."
        });
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
        Assert.NotNull(submitted?.Data);
        var messageId = submitted.Data;

        var adminAuth = await app.LoginAdminAsync();
        using var adminClient = _fixture.Factory.CreateClient().WithBearer(adminAuth.AccessToken);

        // Mark as read
        var markReadResponse = await adminClient.PostAsync(
            $"/api/v1/admin/contact-messages/{messageId}/mark-read",
            null);
        markReadResponse.EnsureSuccessStatusCode();

        // Verify IsRead is now true via the list endpoint
        var listResponse = await adminClient.GetAsync(
            $"/api/v1/admin/contact-messages?page=1&pageSize=50&search={Uri.EscapeDataString(uniqueEmail)}");
        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<ContactMessageListDto>>();
        Assert.NotNull(list?.Data);
        var found = Assert.Single(list.Data.Items, m => m.Id == messageId);
        Assert.True(found.IsRead);
    }

    [SkippableFact]
    public async Task Admin_mark_read_returns_404_for_unknown_id()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);
        var adminAuth = await app.LoginAdminAsync();
        using var adminClient = _fixture.Factory.CreateClient().WithBearer(adminAuth.AccessToken);

        var response = await adminClient.PostAsync(
            $"/api/v1/admin/contact-messages/{Guid.NewGuid()}/mark-read",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Private DTOs mirroring the API envelope (no shared reference needed)
    // -----------------------------------------------------------------------

    private sealed record ContactMessageListDto(
        IReadOnlyList<ContactMessageSummaryDto> Items,
        int TotalCount,
        int Page,
        int PageSize);

    private sealed record ContactMessageSummaryDto(
        Guid Id,
        string Name,
        string Email,
        string Subject,
        string Body,
        bool IsRead,
        DateTime SubmittedAtUtc);
}
