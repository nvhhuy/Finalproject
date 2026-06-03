using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Observability;

/// <summary>Shared Serilog bootstrap for Api, Worker, and future hosts (read from <c>Serilog</c> configuration section).</summary>
public static class PlatformSerilog
{
    private const string IntegrationTestingFlag = "IntegrationTesting:DisableRateLimiting";

    /// <summary>
    ///     Use with <c>builder.Host.UseSerilog(PlatformSerilog.Configure("api"))</c> on the web host builder.
    ///     Adds machine name, service name, environment, and <see cref="LogContext" /> (for scopes and request enrichers).
    /// </summary>
    public static Action<HostBuilderContext, IServiceProvider, LoggerConfiguration> Configure(string serviceName)
    {
        return (context, services, configuration) =>
        {
            if (context.Configuration.GetValue<bool>(IntegrationTestingFlag))
            {
                ConfigureConsoleOnly(configuration, context, serviceName);
                return;
            }

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
    ///     Api integration tests host in Development but must not use the OpenSearch sink from
    ///     appsettings.Development.json (localhost:9200 is absent in CI and can block for minutes).
    /// </summary>
    private static void ConfigureConsoleOnly(
        LoggerConfiguration configuration,
        HostBuilderContext context,
        string serviceName)
    {
        configuration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Service", serviceName)
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
            .WriteTo.Console();
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