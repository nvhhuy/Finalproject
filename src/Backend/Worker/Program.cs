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

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

PlatformSerilog.RegisterForHostApplicationBuilder(builder, "worker");

builder.Services.Configure<WorkerSchedulingOptions>(builder.Configuration.GetSection(WorkerSchedulingOptions.SectionName));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPlatformOpenTelemetry(builder.Configuration, serviceNameFallback: "finalproject.worker");
builder.Services.AddOutboxPublisher(builder.Configuration);
builder.Services.AddSingleton<ICronJob, SampleCronJob>();
builder.Services.AddWorkerScheduling(builder.Configuration);
builder.Services.AddHostedService<SampleQueueConsumerService>();

IConfigurationSection workerSection = builder.Configuration.GetSection(WorkerSchedulingOptions.SectionName);
if (workerSection.GetValue("EnableSampleBackgroundLoop", false))
    builder.Services.AddHostedService<SampleBackgroundService>();

IHost app = builder.Build();
await app.RunAsync();
