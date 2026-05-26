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
