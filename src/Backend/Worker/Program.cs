using Application;
using Application.Contracts.Notifications;
using Application.Contracts.Scheduling;
using Infrastructure;
using Infrastructure.Messaging.Outbox;
using Infrastructure.Modules.Notifications;
using Infrastructure.Scheduling;
using Observability;
using Worker;
using Worker.Billing;
using Worker.Email;
using Worker.Media;
using Worker.Notifications;
using Worker.Outbox;
using Worker.Samples;
using Worker.Settings;

#if DEBUG
// IDE/Rider often launch bin/Debug/.../Worker without DOTNET_ENVIRONMENT (defaults to Production
// and trips the DataProtection fail-fast). Match Api launchSettings: Development for local runs.
EnsureDevelopmentEnvironmentWhenUnset();
#endif

var builder = Host.CreateApplicationBuilder(args);

PlatformSerilog.RegisterForHostApplicationBuilder(builder, "worker");

builder.Services.Configure<WorkerSchedulingOptions>(
    builder.Configuration.GetSection(WorkerSchedulingOptions.SectionName));
builder.Services.AddApplication();
builder.Services.AddInfrastructureCore(builder.Configuration);
builder.Services.AddSingleton<INotificationDispatcher, NullNotificationDispatcher>();
builder.Services.AddPlatformOpenTelemetry(builder.Configuration, "baseproject.worker");
builder.Services.AddOutboxPublisher(builder.Configuration);

// Production cron jobs. Samples below are gated behind config so they don't pollute the
// running binary; flip `Worker:EnableSamples` to true to spin them up as scaffolding when
// learning the codebase.
builder.Services.AddSingleton<ICronJob, StaleMediaRecoveryJob>();
builder.Services.AddSingleton<ICronJob, MediaStorageCleanupJob>();
builder.Services.AddSingleton<ICronJob, OutboxCleanupJob>();
builder.Services.AddSingleton<ICronJob, SystemSettingsBackupJob>();
builder.Services.AddSingleton<ICronJob, DatabaseBackupJob>();
builder.Services.AddSingleton<ICronJob, SubscriptionDunningJob>();
builder.Services.AddSingleton<ICronJob, SubscriptionCreditGrantJob>();
builder.Services.AddSingleton<ICronJob, NotificationDeliveryJob>();
builder.Services.AddWorkerScheduling(builder.Configuration);
builder.Services.AddHostedService<MediaProcessingConsumerService>();
builder.Services.AddHostedService<EmailSendConsumerService>();

var workerSection = builder.Configuration.GetSection(WorkerSchedulingOptions.SectionName);
if (workerSection.GetValue("EnableSamples", false))
{
    builder.Services.AddSingleton<ICronJob, SampleCronJob>();
    builder.Services.AddHostedService<SampleQueueConsumerService>();
    if (workerSection.GetValue("EnableSampleBackgroundLoop", false))
        builder.Services.AddHostedService<SampleBackgroundService>();
}

var app = builder.Build();
await app.RunAsync();

#if DEBUG
static void EnsureDevelopmentEnvironmentWhenUnset()
{
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"))
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
        return;

    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
}
#endif