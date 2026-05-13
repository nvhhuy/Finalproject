using Application.Contracts.Scheduling;
using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Infrastructure.Scheduling;

/// <summary>Lightweight cron host using Cronos. Replace with Quartz.NET / Hangfire for persistence, misfire handling, and clustering.</summary>
public sealed class CronJobSchedulerHostedService : BackgroundService
{
    private readonly IEnumerable<ICronJob> _jobs;
    private readonly ILogger<CronJobSchedulerHostedService> _logger;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRunUtc = new();

    public CronJobSchedulerHostedService(IEnumerable<ICronJob> jobs, ILogger<CronJobSchedulerHostedService> logger)
    {
        _jobs = jobs;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<(ICronJob job, CronExpression cron)> schedules = new();
        foreach (ICronJob job in _jobs)
        {
            try
            {
                CronExpression expr = CronExpression.Parse(job.CronExpression, CronFormat.Standard);
                schedules.Add((job, expr));
                _logger.LogInformation("Cron job registered: {Name} ({Cron})", job.Name, job.CronExpression);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Skipping job {Name}: invalid cron {Cron}", job.Name, job.CronExpression);
            }
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            DateTimeOffset utcNow = DateTimeOffset.UtcNow;
            foreach ((ICronJob job, CronExpression cron) in schedules)
            {
                DateTimeOffset from = _lastRunUtc.TryGetValue(job.Name, out DateTimeOffset last) ? last : DateTimeOffset.MinValue;
                DateTimeOffset? next = cron.GetNextOccurrence(from, TimeZoneInfo.Utc, inclusive: false);
                if (next is null || next.Value > utcNow)
                    continue;

                try
                {
                    await job.ExecuteAsync(stoppingToken);
                    _lastRunUtc[job.Name] = utcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cron job {Name} failed", job.Name);
                }
            }
        }
    }
}
