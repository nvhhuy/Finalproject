using Application;
using Application.Contracts.Scheduling;
using Infrastructure;
using Infrastructure.Messaging.Outbox;
using Infrastructure.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Observability;
using Serilog;
using Worker;
using Worker.Media;

#if DEBUG
// IDE/Rider often launch bin/Debug/.../Worker without DOTNET_ENVIRONMENT (defaults to Production
// and trips the DataProtection fail-fast). Match Api launchSettings: Development for local runs.
EnsureDevelopmentEnvironmentWhenUnset();
#endif

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

PlatformSerilog.RegisterForHostApplicationBuilder(builder, "worker");

builder.Services.Configure<WorkerSchedulingOptions>(builder.Configuration.GetSection(WorkerSchedulingOptions.SectionName));
builder.Services.AddApplication();
builder.Services.AddInfrastructureCore(builder.Configuration);
builder.Services.AddPlatformOpenTelemetry(builder.Configuration, serviceNameFallback: "finalproject.worker");
builder.Services.AddOutboxPublisher(builder.Configuration);

// Production cron jobs. Samples below are gated behind config so they don't pollute the
// running binary; flip `Worker:EnableSamples` to true to spin them up as scaffolding when
// learning the codebase.
builder.Services.AddSingleton<ICronJob, Worker.Media.StaleMediaRecoveryJob>();
builder.Services.AddSingleton<ICronJob, Worker.Media.MediaStorageCleanupJob>();
builder.Services.AddSingleton<ICronJob, Worker.Outbox.OutboxCleanupJob>();
builder.Services.AddWorkerScheduling(builder.Configuration);
builder.Services.AddHostedService<MediaProcessingConsumerService>();

IConfigurationSection workerSection = builder.Configuration.GetSection(WorkerSchedulingOptions.SectionName);
if (workerSection.GetValue("EnableSamples", false))
{
    builder.Services.AddSingleton<ICronJob, Worker.Samples.SampleCronJob>();
    builder.Services.AddHostedService<Worker.Samples.SampleQueueConsumerService>();
    if (workerSection.GetValue("EnableSampleBackgroundLoop", false))
        builder.Services.AddHostedService<Worker.Samples.SampleBackgroundService>();
}

IHost app = builder.Build();
await app.RunAsync();

#if DEBUG
static void EnsureDevelopmentEnvironmentWhenUnset()
{
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"))
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
    {
        return;
    }

    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
}
#endif
