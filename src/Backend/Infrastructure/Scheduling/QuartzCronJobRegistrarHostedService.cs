using Application.Contracts.Scheduling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Infrastructure.Scheduling;

/// <summary>Registers all <see cref="ICronJob" /> instances with the Quartz scheduler after the host starts.</summary>
public sealed class QuartzCronJobRegistrarHostedService : IHostedService
{
    private readonly IEnumerable<ICronJob> _jobs;
    private readonly ILogger<QuartzCronJobRegistrarHostedService> _logger;
    private readonly ISchedulerFactory _schedulerFactory;

    public QuartzCronJobRegistrarHostedService(
        ISchedulerFactory schedulerFactory,
        IEnumerable<ICronJob> jobs,
        ILogger<QuartzCronJobRegistrarHostedService> logger)
    {
        _schedulerFactory = schedulerFactory;
        _jobs = jobs;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        foreach (var cron in _jobs)
        {
            string quartzCron;
            try
            {
                quartzCron = QuartzCronExpression.ToQuartz(cron.CronExpression);
            }
            catch (FormatException ex)
            {
                _logger.LogError(
                    ex,
                    "Skipping Quartz job {Name}: invalid cron {Cron}",
                    cron.Name,
                    cron.CronExpression);
                continue;
            }

            var jobKey = new JobKey(cron.Name);
            var detail = JobBuilder.Create<QuartzCronJobBridge>()
                .WithIdentity(jobKey)
                .UsingJobData(QuartzCronJobBridge.JobNameKey, cron.Name)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"{cron.Name}-trigger")
                .ForJob(jobKey)
                .WithCronSchedule(quartzCron, b => b.InTimeZone(TimeZoneInfo.Utc))
                .Build();

            if (await scheduler.CheckExists(jobKey, cancellationToken))
                await scheduler.DeleteJob(jobKey, cancellationToken);

            await scheduler.ScheduleJob(detail, trigger, cancellationToken);
            _logger.LogInformation(
                "Quartz job registered: {Name} ({Cron} -> {QuartzCron})",
                cron.Name,
                cron.CronExpression,
                quartzCron);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}