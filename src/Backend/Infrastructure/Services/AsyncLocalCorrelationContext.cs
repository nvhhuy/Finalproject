using Application.Contracts.Services;

namespace Infrastructure.Services;

/// <summary>
///     <see cref="ICorrelationContext" /> backed by <see cref="AsyncLocal{T}" />. Singleton because the
///     state itself is per-async-flow — no per-scope DI needed, and resolving from singletons (e.g.
///     the singleton outbox publisher) is required.
/// </summary>
public sealed class AsyncLocalCorrelationContext : ICorrelationContext
{
    private static readonly AsyncLocal<string?> _current = new();

    public string? Current => _current.Value;

    public IDisposable BeginScope(string correlationId)
    {
        var previous = _current.Value;
        _current.Value = correlationId;
        return new Restorer(previous);
    }

    private sealed class Restorer : IDisposable
    {
        private readonly string? _previous;

        public Restorer(string? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            _current.Value = _previous;
        }
    }
}