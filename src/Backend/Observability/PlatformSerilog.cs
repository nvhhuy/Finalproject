using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Observability;

/// <summary>Shared Serilog bootstrap for Api, Worker, and future hosts (read from <c>Serilog</c> configuration section).</summary>
public static class PlatformSerilog
{
    /// <summary>
    ///     Use with <c>builder.Host.UseSerilog(PlatformSerilog.Configure("api"))</c> on the web host builder.
    ///     Adds machine name, service name, environment, and <see cref="LogContext" /> (for scopes and request enrichers).
    /// </summary>
    public static Action<HostBuilderContext, IServiceProvider, LoggerConfiguration> Configure(string serviceName)
    {
        return (context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("Service", serviceName)
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName);
        };
    }

    /// <summary>
    ///     <see cref="HostApplicationBuilder" /> has no <c>Host</c>; register Serilog on Microsoft logging before
    ///     <c>Build()</c>.
    /// </summary>
    public static void RegisterForHostApplicationBuilder(HostApplicationBuilder builder, string serviceName)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Service", serviceName)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, true);
    }
}