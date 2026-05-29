using System.Text;
using Application.Contracts.Messaging;
using Infrastructure.Messaging.Outbox;
using Infrastructure.Persistence;
using Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.Tests.Messaging;

public sealed class OutboxPublisherHostedServiceTests
{
    [Fact]
    public async Task ProcessBatch_publishes_pending_rows_and_marks_them_processed()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        var publisher = new CapturingPublisher();
        var row = AddRow(db, "user.registered.v1", "{\"id\":1}");
        await db.SaveChangesAsync();

        var processed = await OutboxPublisherHostedService.ProcessBatchAsync(
            db, publisher, NewOptions(), NullLogger.Instance, CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Single(publisher.Calls);
        Assert.Equal("user.registered.v1", publisher.Calls[0].Context.RoutingKey);
        Assert.Equal("{\"id\":1}", Encoding.UTF8.GetString(publisher.Calls[0].Payload));

        var reloaded = await db.OutboxMessages.SingleAsync(m => m.Id == row.Id);
        Assert.NotNull(reloaded.ProcessedOnUtc);
        Assert.Equal(1, reloaded.Attempts);
        Assert.Null(reloaded.Error);
    }

    [Fact]
    public async Task ProcessBatch_skips_already_processed_rows()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        var publisher = new CapturingPublisher();
        var row = AddRow(db);
        row.MarkProcessed(DateTime.UtcNow.AddMinutes(-1));
        await db.SaveChangesAsync();

        var processed = await OutboxPublisherHostedService.ProcessBatchAsync(
            db, publisher, NewOptions(), NullLogger.Instance, CancellationToken.None);

        Assert.Equal(0, processed);
        Assert.Empty(publisher.Calls);
    }

    [Fact]
    public async Task ProcessBatch_records_failure_and_schedules_retry_when_publish_throws()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        var publisher = new ThrowingPublisher(new InvalidOperationException("broker down"));
        var row = AddRow(db);
        await db.SaveChangesAsync();

        await OutboxPublisherHostedService.ProcessBatchAsync(
            db, publisher, NewOptions(initialBackoff: 10, maxAttempts: 5), NullLogger.Instance, CancellationToken.None);

        var reloaded = await db.OutboxMessages.SingleAsync(m => m.Id == row.Id);
        Assert.Null(reloaded.ProcessedOnUtc);
        Assert.Equal(1, reloaded.Attempts);
        Assert.Equal("broker down", reloaded.Error);
        Assert.NotNull(reloaded.NextAttemptUtc);
        var delay = reloaded.NextAttemptUtc!.Value - DateTime.UtcNow;
        Assert.InRange(delay.TotalSeconds, 5, 15);
    }

    [Fact]
    public async Task ProcessBatch_backoff_grows_exponentially_with_attempts()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        var publisher = new ThrowingPublisher(new InvalidOperationException("nope"));
        var row = AddRow(db);
        // simulate two prior failures
        row.RecordFailure(DateTime.UtcNow.AddMinutes(-5), "earlier", TimeSpan.FromSeconds(1));
        row.RecordFailure(DateTime.UtcNow.AddMinutes(-3), "earlier", TimeSpan.FromSeconds(1));
        // force eligibility for processing
        typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.NextAttemptUtc))!.SetValue(row,
            DateTime.UtcNow.AddSeconds(-1));
        await db.SaveChangesAsync();

        await OutboxPublisherHostedService.ProcessBatchAsync(
            db, publisher, NewOptions(initialBackoff: 10, maxAttempts: 10), NullLogger.Instance,
            CancellationToken.None);

        var reloaded = await db.OutboxMessages.SingleAsync(m => m.Id == row.Id);
        Assert.Equal(3, reloaded.Attempts);
        var delay = reloaded.NextAttemptUtc!.Value - DateTime.UtcNow;
        // initial 10s * 2^2 = 40s for the 3rd attempt
        Assert.InRange(delay.TotalSeconds, 35, 45);
    }

    [Fact]
    public async Task ProcessBatch_abandons_row_after_max_attempts_exceeded()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        var publisher = new ThrowingPublisher(new InvalidOperationException("final fail"));
        var row = AddRow(db);
        // 2 prior failures, MaxAttempts=3 — this run hits the cap
        for (var i = 0; i < 2; i++)
            row.RecordFailure(DateTime.UtcNow.AddMinutes(-1), "earlier", TimeSpan.FromSeconds(1));
        typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.NextAttemptUtc))!.SetValue(row,
            DateTime.UtcNow.AddSeconds(-1));
        await db.SaveChangesAsync();

        await OutboxPublisherHostedService.ProcessBatchAsync(
            db, publisher, NewOptions(initialBackoff: 10, maxAttempts: 3), NullLogger.Instance, CancellationToken.None);

        var reloaded = await db.OutboxMessages.SingleAsync(m => m.Id == row.Id);
        Assert.Equal(3, reloaded.Attempts);
        // After M7 fix: abandoned rows now set AbandonedAtUtc (not ProcessedOnUtc) so the
        // publisher loop can skip them while keeping them separable from successful publishes.
        Assert.Null(reloaded.ProcessedOnUtc);
        Assert.NotNull(reloaded.AbandonedAtUtc);
        Assert.Equal("final fail", reloaded.Error);
        Assert.Null(reloaded.NextAttemptUtc);
    }

    [Fact]
    public async Task ProcessBatch_respects_NextAttemptUtc_window()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        var publisher = new CapturingPublisher();
        var row = AddRow(db);
        row.RecordFailure(DateTime.UtcNow, "earlier", TimeSpan.FromMinutes(10));
        await db.SaveChangesAsync();

        var processed = await OutboxPublisherHostedService.ProcessBatchAsync(
            db, publisher, NewOptions(), NullLogger.Instance, CancellationToken.None);

        Assert.Equal(0, processed);
        Assert.Empty(publisher.Calls);
    }

    [Fact]
    public async Task ProcessBatch_honours_BatchSize_limit()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        var publisher = new CapturingPublisher();
        for (var i = 0; i < 5; i++)
            AddRow(db, occurredOffset: TimeSpan.FromSeconds(-i));
        await db.SaveChangesAsync();

        var processed = await OutboxPublisherHostedService.ProcessBatchAsync(
            db, publisher, NewOptions(2), NullLogger.Instance, CancellationToken.None);

        Assert.Equal(2, processed);
        Assert.Equal(2, publisher.Calls.Count);
        Assert.Equal(2, await db.OutboxMessages.CountAsync(m => m.ProcessedOnUtc != null));
    }

    private static OutboxMessage AddRow(
        ApplicationDbContext db,
        string routingKey = "test.event.v1",
        string payload = "{}",
        TimeSpan? occurredOffset = null)
    {
        var row = new OutboxMessage(
            Guid.NewGuid(),
            DateTime.UtcNow + (occurredOffset ?? TimeSpan.Zero),
            "Test.Type, Test.Assembly",
            routingKey,
            null,
            payload);
        db.OutboxMessages.Add(row);
        return row;
    }

    private static OutboxOptions NewOptions(int batchSize = 50, int initialBackoff = 10, int maxAttempts = 10)
    {
        return new OutboxOptions
        {
            Enabled = true,
            BatchSize = batchSize,
            InitialBackoffSeconds = initialBackoff,
            MaxAttempts = maxAttempts,
            PollIntervalSeconds = 1
        };
    }

    private sealed class CapturingPublisher : IMessagePublisher
    {
        public List<(byte[] Payload, MessagePublishContext Context)> Calls { get; } = new();

        public Task PublishAsync<TMessage>(TMessage message, MessagePublishContext context,
            CancellationToken cancellationToken = default)
            where TMessage : class
        {
            throw new NotSupportedException("outbox uses PublishRawAsync");
        }

        public Task PublishRawAsync(ReadOnlyMemory<byte> payload, MessagePublishContext context,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((payload.ToArray(), context));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher : IMessagePublisher
    {
        private readonly Exception _ex;

        public ThrowingPublisher(Exception ex)
        {
            _ex = ex;
        }

        public Task PublishAsync<TMessage>(TMessage message, MessagePublishContext context,
            CancellationToken cancellationToken = default)
            where TMessage : class
        {
            throw _ex;
        }

        public Task PublishRawAsync(ReadOnlyMemory<byte> payload, MessagePublishContext context,
            CancellationToken cancellationToken = default)
        {
            throw _ex;
        }
    }
}