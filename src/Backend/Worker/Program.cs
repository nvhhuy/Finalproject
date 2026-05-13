using Application;
using Application.Contracts.Scheduling;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Observability;
using Serilog;
using Worker;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

PlatformSerilog.RegisterForHostApplicationBuilder(builder, "worker");

builder.Services.Configure<WorkerSchedulingOptions>(builder.Configuration.GetSection(WorkerSchedulingOptions.SectionName));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ICronJob, SampleCronJob>();
builder.Services.AddCronScheduling();
builder.Services.AddHostedService<SampleQueueConsumerService>();

IHost app = builder.Build();
await app.RunAsync();
