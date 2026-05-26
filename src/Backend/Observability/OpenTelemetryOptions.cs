namespace Observability;

/// <summary>Configuration for <see cref="OpenTelemetryRegistration.AddPlatformOpenTelemetry"/>.</summary>
public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    /// <summary>Logical service name used in <c>service.name</c> attribute and traces.</summary>
    public string ServiceName { get; set; } = "finalproject";

    /// <summary>Version reported as <c>service.version</c>.</summary>
    public string? ServiceVersion { get; set; }

    /// <summary>Disable to opt out entirely.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Toggles distributed tracing pipeline.</summary>
    public bool Tracing { get; set; } = true;

    /// <summary>Toggles metrics pipeline.</summary>
    public bool Metrics { get; set; } = true;

    /// <summary>OTLP collector endpoint, e.g. <c>http://otel-collector:4317</c>. When omitted, OTLP export is skipped.</summary>
    public string? OtlpEndpoint { get; set; }
}
