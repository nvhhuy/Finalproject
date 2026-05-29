using Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Infrastructure.HealthChecks;

/// <summary>Opens a short-lived RabbitMQ connection to verify reachability and credentials.</summary>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IOptionsMonitor<RabbitMqOptions> _options;

    public RabbitMqHealthCheck(IOptionsMonitor<RabbitMqOptions> options)
    {
        _options = options;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var o = _options.CurrentValue;
        if (!o.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ disabled by configuration."));

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = o.Host,
                Port = o.Port,
                UserName = o.UserName,
                Password = o.Password,
                VirtualHost = o.VirtualHost,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(3),
                AutomaticRecoveryEnabled = false
            };
            using var connection = factory.CreateConnection("healthcheck");
            return Task.FromResult(connection.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("RabbitMQ connection not open."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ connect failed.", ex));
        }
    }
}