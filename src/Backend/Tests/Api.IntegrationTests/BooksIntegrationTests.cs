using System.Net;
using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Tenancy;
using Domains.Tenancy;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class BooksIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public BooksIntegrationTests(IntegrationAppFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Anonymous_request_is_rejected()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var response = await app.Client.GetAsync("/api/v1/books");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task User_can_run_full_book_crud_lifecycle()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (auth, _) = await app.RegisterVerifyAndLoginAsync();
        using var client = app.Client.WithBearer(auth.AccessToken);
        client.DefaultRequestHeaders.Add(TenantHttpHeaders.TenantId, SystemTenants.DefaultTenantId.ToString());

        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/books")
        {
            Content = JsonContent.Create(new
            {
                title = "Domain-Driven Design",
                author = "Eric Evans",
                isbn = "978-0321125217",
                description = "Tackling complexity in the heart of software.",
                publishedYear = 2003
            })
        }.WithIdempotencyKey();

        var createResponse = await client.SendAsync(create);
        var created = await createResponse.ReadApiAsync<BookDto>();
        Assert.NotNull(created?.Data);
        var bookId = created.Data.Id;
        Assert.Equal("Domain-Driven Design", created.Data.Title);

        var listResponse = await client.GetAsync("/api/v1/books?page=1&pageSize=20&search=domain");
        var list = await listResponse.ReadApiAsync<BookListDto>();
        Assert.NotNull(list?.Data);
        Assert.Contains(list.Data.Items, b => b.Id == bookId);

        var getResponse = await client.GetAsync($"/api/v1/books/{bookId}");
        var fetched = await getResponse.ReadApiAsync<BookDto>();
        Assert.NotNull(fetched?.Data);
        Assert.Equal("Eric Evans", fetched.Data.Author);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/books/{bookId}", new
        {
            title = "Domain-Driven Design (2nd read)",
            author = "Eric Evans",
            isbn = "978-0321125217",
            description = "Updated notes.",
            publishedYear = 2004,
            rowVersion = fetched.Data.RowVersion
        });
        var updated = await updateResponse.ReadApiAsync<BookDto>();
        Assert.NotNull(updated?.Data);
        Assert.Equal("Domain-Driven Design (2nd read)", updated.Data.Title);
        Assert.Equal(2004, updated.Data.PublishedYear);

        // Stale row version must be rejected with a conflict.
        var staleUpdateResponse = await client.PutAsJsonAsync($"/api/v1/books/{bookId}", new
        {
            title = "Should not apply",
            author = "Eric Evans",
            isbn = (string?)null,
            description = (string?)null,
            publishedYear = (int?)null,
            rowVersion = fetched.Data.RowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, staleUpdateResponse.StatusCode);

        var deleteResponse =
            await client.DeleteAsync($"/api/v1/books/{bookId}?rowVersion={updated.Data.RowVersion}");
        deleteResponse.EnsureSuccessStatusCode();

        var getAfterDeleteResponse = await client.GetAsync($"/api/v1/books/{bookId}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDeleteResponse.StatusCode);
    }

    [SkippableFact]
    public async Task User_cannot_access_another_users_book()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (ownerAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using var ownerClient = app.Client.WithBearer(ownerAuth.AccessToken);
        ownerClient.DefaultRequestHeaders.Add(TenantHttpHeaders.TenantId, SystemTenants.DefaultTenantId.ToString());

        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/books")
        {
            Content = JsonContent.Create(new
            {
                title = "Private Book",
                author = "Someone",
                isbn = (string?)null,
                description = (string?)null,
                publishedYear = (int?)null
            })
        }.WithIdempotencyKey();
        var createResponse = await ownerClient.SendAsync(create);
        var created = await createResponse.ReadApiAsync<BookDto>();
        Assert.NotNull(created?.Data);

        var (otherAuth, _) = await app.RegisterVerifyAndLoginAsync();
        using var otherClient = _fixture.Factory.CreateClient().WithBearer(otherAuth.AccessToken);
        otherClient.DefaultRequestHeaders.Add(TenantHttpHeaders.TenantId, SystemTenants.DefaultTenantId.ToString());

        var getResponse = await otherClient.GetAsync($"/api/v1/books/{created.Data.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    private sealed record BookDto(
        Guid Id,
        string Title,
        string Author,
        string? Isbn,
        string? Description,
        int? PublishedYear,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        uint RowVersion);

    private sealed record BookListDto(IReadOnlyList<BookDto> Items, int TotalCount, int Page, int PageSize);
}
