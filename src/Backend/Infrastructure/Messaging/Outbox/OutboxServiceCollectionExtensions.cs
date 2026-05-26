using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Messaging.Outbox;

public static class OutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the transactional-outbox publisher hosted service. Call from the Worker host
    /// only — the API process should not drain the outbox (avoid double publishes).
    /// </summary>
    public static IServiceCollection AddOutboxPublisher(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddHostedService<OutboxPublisherHostedService>();
        return services;
    }
}
