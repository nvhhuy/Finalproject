using Application.Contracts.Messaging;
using Application.Contracts.Services;
using Domains.Common.Interfaces;
using Infrastructure.Persistence;
using System.Text.Json;

namespace Infrastructure.Messaging.Outbox;

public sealed class IntegrationEventOutboxWriter : IIntegrationEventOutboxWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ApplicationDbContext _db;
    private readonly ICorrelationContext _correlation;

    public IntegrationEventOutboxWriter(ApplicationDbContext db, ICorrelationContext correlation)
    {
        _db = db;
        _correlation = correlation;
    }

    public async Task EnqueueAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        string payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions);
        var row = new OutboxMessage(
            eventId: integrationEvent.EventId,
            occurredOnUtc: integrationEvent.OccurredOnUtc,
            type: integrationEvent.GetType().AssemblyQualifiedName ?? integrationEvent.GetType().FullName!,
            routingKey: integrationEvent.RoutingKey,
            exchange: null,
            payloadJson: payload,
            correlationId: _correlation.Current);

        _db.OutboxMessages.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
