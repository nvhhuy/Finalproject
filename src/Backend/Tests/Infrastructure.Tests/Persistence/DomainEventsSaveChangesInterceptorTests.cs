using System.Text.Json;
using Domains.UserAggregate;
using Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Persistence;

public sealed class DomainEventsSaveChangesInterceptorTests
{
    [Fact]
    public async Task Integration_event_is_persisted_to_outbox_inside_save()
    {
        using var db = TestDbContextFactory.Create(out var publisher, out _);

        var user = NewUser();
        var integration = new TestIntegrationEvent(user.Id.Value);
        user.AddDomainEvent(integration);

        db.Users.Add(user);
        var affected = await db.SaveChangesAsync();

        Assert.True(affected > 0);

        var rows = await db.OutboxMessages.ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(integration.EventId, row.EventId);
        Assert.Equal("test.integration.v1", row.RoutingKey);
        Assert.Contains(user.Id.Value.ToString(), row.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Null(row.ProcessedOnUtc);
        Assert.Equal(0, row.Attempts);

        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task Integration_event_payload_is_valid_json_of_concrete_type()
    {
        using var db = TestDbContextFactory.Create(out _, out _);

        var user = NewUser();
        var integration = new TestIntegrationEvent(user.Id.Value);
        user.AddDomainEvent(integration);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var row = await db.OutboxMessages.SingleAsync();
        var round = JsonSerializer.Deserialize<TestIntegrationEvent>(
            row.PayloadJson,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.NotNull(round);
        Assert.Equal(integration.EventId, round!.EventId);
        Assert.Equal(integration.UserId, round.UserId);
    }

    [Fact]
    public async Task After_commit_event_does_not_create_outbox_row()
    {
        using var db = TestDbContextFactory.Create(out var publisher, out _);

        var user = NewUser();
        user.AddDomainEvent(new TestAfterCommitEvent(user.Id.Value));

        db.Users.Add(user);
        await db.SaveChangesAsync();

        Assert.Empty(await db.OutboxMessages.ToListAsync());
        Assert.Single(publisher.Published);
        Assert.IsType<TestAfterCommitEvent>(publisher.Published[0]);
    }

    [Fact]
    public async Task Transactional_event_is_published_in_process_and_skips_outbox()
    {
        using var db = TestDbContextFactory.Create(out var publisher, out _);

        var user = NewUser();
        user.AddDomainEvent(new TestTransactionalEvent(user.Id.Value));

        db.Users.Add(user);
        await db.SaveChangesAsync();

        Assert.Empty(await db.OutboxMessages.ToListAsync());
        Assert.Single(publisher.Published);
        Assert.IsType<TestTransactionalEvent>(publisher.Published[0]);
    }

    [Fact]
    public async Task Domain_events_collection_is_cleared_after_save()
    {
        using var db = TestDbContextFactory.Create(out _, out _);

        var user = NewUser();
        user.AddDomainEvent(new TestTransactionalEvent(user.Id.Value));
        user.AddDomainEvent(new TestIntegrationEvent(user.Id.Value));

        db.Users.Add(user);
        await db.SaveChangesAsync();

        Assert.Empty(user.DomainEvents);
    }

    private static User NewUser()
    {
        return User.CreateNew("A", "B", $"u{Guid.NewGuid():N}"[..12], $"{Guid.NewGuid():N}@example.com",
            "hash:placeholder");
    }
}