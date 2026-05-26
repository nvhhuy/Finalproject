using Application.Contracts.Events;
using Application.Contracts.Services;
using Domains.UserAggregate.ValueObjects;
using Infrastructure.Modules.Users.Persistence.Conversions;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Events;
using Infrastructure.Persistence.Interceptors;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Support;

/// <summary>Builds an <see cref="ApplicationDbContext"/> backed by EF Core InMemory for fast in-process tests.</summary>
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

        var auditable = new AuditableInterceptor(new StubClock(), new StubLoggedInUserService());
        var domainEvents = new DomainEventsSaveChangesInterceptor(publisher, afterCommitQueue);

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options, auditable, domainEvents);
    }

    private sealed class StubClock : IDateTimeProvider
    {
        public DateTime Now => DateTime.UtcNow;
    }

    private sealed class StubLoggedInUserService : ILoggedInUserService
    {
        public UserId? UserId => null;
        public bool IsInRole(string role) => false;
    }
}

/// <summary>MediatR <see cref="IPublisher"/> stub that records every notification it sees.</summary>
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
