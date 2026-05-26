using Application.Contracts.Messaging;

namespace Infrastructure.Messaging;

/// <summary>
/// Registered when RabbitMQ is disabled. Returning an empty list (instead of throwing) lets the
/// admin UI render the management page without RabbitMQ being available — useful for local dev.
/// </summary>
public sealed class NullDeadLetterAdminService : IDeadLetterAdminService
{
    public Task<IReadOnlyList<DeadLetterMessage>> PeekAsync(string queueName, int maxMessages, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DeadLetterMessage>>(Array.Empty<DeadLetterMessage>());

    public Task<int> ReplayAsync(string queueName, int maxMessages, CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<int> DrainAsync(string queueName, int maxMessages, CancellationToken cancellationToken = default) => Task.FromResult(0);
}
