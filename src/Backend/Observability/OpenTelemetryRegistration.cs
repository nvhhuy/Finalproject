using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Observability;

/// <summary>
/// Wires distributed tracing + runtime metrics for ASP.NET, HttpClient, EF Core and Redis.
/// Activity sources and meters are exported via OTLP when an endpoint is configured.
/// </summary>
public static class OpenTelemetryRegistration
{
    /// <summary>Custom <c>ActivitySource</c> used by handlers/services for manual spans.</summary>
    public const string ApplicationActivitySource = "FinalProject.Application";

    /// <summary>Custom <c>Meter</c> used by application code for counters/histograms.</summary>
    public const string ApplicationMeterName = "FinalProject.Application";

    public static IServiceCollection AddPlatformOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceNameFallback)
    {
        OpenTelemetryOptions opts = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
            ?? new OpenTelemetryOptions();
        if (!opts.Enabled)
            return services;

        string serviceName = string.IsNullOrWhiteSpace(opts.ServiceName) ? serviceNameFallback : opts.ServiceName;

        ResourceBuilder resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(serviceName: serviceName, serviceVersion: opts.ServiceVersion ?? "1.0.0");

        IOpenTelemetryBuilder builder = services.AddOpenTelemetry().ConfigureResource(_ => { });

        if (opts.Tracing)
        {
            builder.WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(ApplicationActivitySource)
                    .AddAspNetCoreInstrumentation(o => o.RecordException = true)
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation(o => o.SetDbStatementForText = true);

                if (!string.IsNullOrWhiteSpace(opts.OtlpEndpoint))
                    tracing.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(opts.OtlpEndpoint));
            });
        }

        if (opts.Metrics)
        {
            builder.WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(ApplicationMeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (!string.IsNullOrWhiteSpace(opts.OtlpEndpoint))
                    metrics.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(opts.OtlpEndpoint));
            });
        }

        return services;
    }
}
