using Application.Contracts.Events;
using Application.Contracts.Tenancy;
using Infrastructure.Modules.Users.Persistence.Conversions;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Events;
using Infrastructure.Persistence.Interceptors;
using Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Tests.Support;

/// <summary>Builds an <see cref="ApplicationDbContext" /> backed by EF Core InMemory for fast in-process tests.</summary>
internal static class TestDbContextFactory
{
    static TestDbContextFactory()
    {
        UserDataProtection.Initialize(new EphemeralDataProtectionProvider());
    }

    public static ApplicationDbContext Create(
        out RecordingPublisher publisher,
        out IAfterCommitDomainEventQueue afterCommitQueue,
        string? databaseName = null)
    {
        publisher = new RecordingPublisher();
        afterCommitQueue = new AfterCommitDomainEventQueue();

        var domainEvents = new DomainEventsSaveChangesInterceptor(
            publisher, afterCommitQueue, new AsyncLocalCorrelationContext());

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options, domainEvents, new TestTenantContext());
    }
}

internal sealed class TestTenantContext : ITenantContext
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

/// <summary>MediatR <see cref="IPublisher" /> stub that records every notification it sees.</summary>
internal sealed class RecordingPublisher : IPublisher
{
    private readonly List<INotification> _published = new();

    public IReadOnlyList<INotification> Published => _published;

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        if (notification is not null)
            _published.Add(notification);
        return Task.CompletedTask;
    }

    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        if (notification is INotification n)
            _published.Add(n);
        return Task.CompletedTask;
    }
}