using System.Text.Json;
using Application.Contracts.Messaging;
using Application.Contracts.Services;
using Domains.Common.Interfaces;
using Infrastructure.Persistence;

namespace Infrastructure.Messaging.Outbox;

public sealed class IntegrationEventOutboxWriter : IIntegrationEventOutboxWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ICorrelationContext _correlation;

    private readonly ApplicationDbContext _db;

    public IntegrationEventOutboxWriter(ApplicationDbContext db, ICorrelationContext correlation)
    {
        _db = db;
        _correlation = correlation;
    }

    public async Task EnqueueAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions);
        var row = new OutboxMessage(
            integrationEvent.EventId,
            integrationEvent.OccurredOnUtc,
            integrationEvent.GetType().AssemblyQualifiedName ?? integrationEvent.GetType().FullName!,
            integrationEvent.RoutingKey,
            null,
            payload,
            _correlation.Current);

        _db.OutboxMessages.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
    }
}