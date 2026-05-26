using Domains.Common.Interfaces;

namespace Domains.MediaAggregate.Events;

/// <summary>
/// Domain marker raised when the processing pipeline finishes successfully and at least one
/// variant exists. The application layer attaches an <see cref="IAfterCommitDomainEvent"/> +
/// <c>INotification</c> wrapper when listeners (timeline projection, push notifier) need to react;
/// keeping this type free of MediatR avoids dragging that dependency into <c>Domains</c>.
/// </summary>
public sealed record MediaAssetReadyDomainEvent(Guid MediaAssetId, Guid OwnerUserId) : IDomainEvent;
