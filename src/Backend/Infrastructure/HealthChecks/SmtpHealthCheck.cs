using System.Net.Sockets;
using Infrastructure.Email;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Infrastructure.HealthChecks;

/// <summary>Lightweight TCP probe against the configured SMTP host:port (no AUTH).</summary>
public sealed class SmtpHealthCheck : IHealthCheck
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);
    private readonly IOptionsMonitor<SmtpEmailOptions> _options;

    public SmtpHealthCheck(IOptionsMonitor<SmtpEmailOptions> options)
    {
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var o = _options.CurrentValue;
        if (!o.Enabled)
            return HealthCheckResult.Healthy("SMTP disabled by configuration.");

        try
        {
            using var client = new TcpClient();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(ConnectTimeout);
            await client.ConnectAsync(o.Host, o.Port, linkedCts.Token);
            return client.Connected
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"SMTP host {o.Host}:{o.Port} did not accept connection.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy($"SMTP connect timed out after {ConnectTimeout.TotalSeconds:0.#}s.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SMTP probe failed.", ex);
        }
    }
}