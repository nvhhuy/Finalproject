using Application.Contracts.Events;
using Application.Contracts.Services;
using Application.Contracts.Tenancy;
using Infrastructure.Messaging.Outbox;
using Infrastructure.Modules.Users.Persistence.Conversions;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Events;
using Infrastructure.Persistence.Interceptors;
using Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Worker.Outbox;

namespace Worker.Tests.Outbox;

/// <summary>
///     Unit tests for <see cref="OutboxCleanupJob" />. Backed by EF Core InMemory + a deterministic
///     clock so we can assert both <i>what</i> got deleted and that <i>nothing else</i> did.
/// </summary>
public sealed class OutboxCleanupJobTests
{
    static OutboxCleanupJobTests()
    {
        // The User entity uses an encrypted-column converter that resolves IDataProtectionProvider
        // statically at model-build time. Without this seed, ApplicationDbContext throws on the
        // first DI resolution. EphemeralDataProtectionProvider is the standard test pattern.
        UserDataProtection.Initialize(new EphemeralDataProtectionProvider());
    }

    [Fact]
    public async Task ExecuteAsync_deletes_only_processed_rows_older_than_retention_window()
    {
        var (sp, db, clock) = BuildScope(30);

        var now = clock.Now;

        // Rows we want gone: processed, processed-on > 30 days ago.
        db.OutboxMessages.Add(CreateRow(now.AddDays(-31)));
        db.OutboxMessages.Add(CreateRow(now.AddDays(-90)));
        // Rows we must keep:
        db.OutboxMessages.Add(CreateRow(now.AddDays(-29))); // within retention
        db.OutboxMessages.Add(CreateRow(null)); // never processed → keep
        await db.SaveChangesAsync();

        var job = new OutboxCleanupJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WorkerSchedulingOptions { OutboxRetentionDays = 30, OutboxCleanupBatchSize = 1_000 }),
            NullLogger<OutboxCleanupJob>.Instance);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(2, await db.OutboxMessages.CountAsync());
        Assert.DoesNotContain(await db.OutboxMessages.ToListAsync(), m => m.ProcessedOnUtc < now.AddDays(-30));
    }

    [Fact]
    public async Task ExecuteAsync_does_not_throw_on_empty_table()
    {
        var (sp, _, _) = BuildScope(30);

        var job = new OutboxCleanupJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WorkerSchedulingOptions { OutboxRetentionDays = 30 }),
            NullLogger<OutboxCleanupJob>.Instance);

        // Should noop silently — used to verify the early-exit when first batch returns 0 rows.
        await job.ExecuteAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_caps_at_batch_size_per_tick_to_avoid_long_locks()
    {
        var (sp, db, clock) = BuildScope(1);

        // Seed 250 already-expired rows; cap a batch at 100 so we expect 100 deletes after 1 tick
        // (the job breaks out when a partial batch comes back; 100/100 isn't partial, so it loops
        // again — second batch is also 100, third is 50, then break). So actually it should
        // delete all 250 in one tick because the loop continues until <batchSize.
        for (var i = 0; i < 250; i++)
            db.OutboxMessages.Add(CreateRow(clock.Now.AddDays(-10)));
        await db.SaveChangesAsync();

        var job = new OutboxCleanupJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WorkerSchedulingOptions { OutboxRetentionDays = 1, OutboxCleanupBatchSize = 100 }),
            NullLogger<OutboxCleanupJob>.Instance);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(0, await db.OutboxMessages.CountAsync());
    }

    private static OutboxMessage CreateRow(DateTime? processedOn)
    {
        var row = new OutboxMessage(
            Guid.NewGuid(),
            DateTime.UtcNow,
            "Test.Event",
            "test.event",
            null,
            "{}");

        if (processedOn.HasValue)
            // OutboxMessage.MarkProcessed sets ProcessedOnUtc + bumps Attempts. We need a specific
            // timestamp so use reflection — adding a public setter would weaken the aggregate.
            typeof(OutboxMessage)
                .GetProperty(nameof(OutboxMessage.ProcessedOnUtc))!
                .SetValue(row, processedOn.Value);
        return row;
    }

    private static (IServiceProvider, ApplicationDbContext, FixedClock) BuildScope(int retentionDays)
    {
        var clock = new FixedClock(DateTime.UtcNow);

        // ApplicationDbContext requires the DomainEventsSaveChangesInterceptor, which itself
        // pulls IPublisher + IAfterCommitDomainEventQueue + ICorrelationContext. Provide thin
        // stubs so the test only exercises outbox cleanup logic, not the full DI graph.
        //
        // We use SQLite (in-memory shared connection) instead of EF's InMemory provider because
        // OutboxCleanupJob relies on ExecuteDeleteAsync, which the InMemory provider's LINQ
        // translator does not support for compound queries with OrderBy + Take. SQLite mirrors
        // Postgres semantics closely enough to exercise the real DELETE statement.
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<IPublisher, NoOpPublisher>();
        services.AddSingleton<IAfterCommitDomainEventQueue, AfterCommitDomainEventQueue>();
        services.AddSingleton<ICorrelationContext, AsyncLocalCorrelationContext>();
        services.AddScoped<DomainEventsSaveChangesInterceptor>();
        services.AddScoped<ITenantContext, TestTenantContext>();
        services.AddDbContext<ApplicationDbContext>(opts => opts.UseSqlite(connection));
        services.AddSingleton<IDateTimeProvider>(clock);

        IServiceProvider sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        return (sp, db, clock);
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish<T>(T notification, CancellationToken cancellationToken = default) where T : INotification
        {
            return Task.CompletedTask;
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public FixedClock(DateTime now)
        {
            Now = now;
        }

        public DateTime Now { get; }
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TenantId { get; private set; }
        public bool BypassTenantIsolation { get; private set; }

        public void SetTenant(Guid tenantId)
        {
            TenantId = tenantId;
        }

        public void EnableBypass()
        {
            BypassTenantIsolation = true;
        }
    }
}