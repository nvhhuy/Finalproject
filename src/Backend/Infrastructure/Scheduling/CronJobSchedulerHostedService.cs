using System.Collections.Concurrent;
using Application.Contracts.Scheduling;
using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Scheduling;

/// <summary>
///     Lightweight cron host using Cronos. Replace with Quartz.NET / Hangfire for persistence, misfire handling, and
///     clustering.
/// </summary>
public sealed class CronJobSchedulerHostedService : BackgroundService
{
    private readonly IEnumerable<ICronJob> _jobs;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRunUtc = new();
    private readonly ILogger<CronJobSchedulerHostedService> _logger;

    public CronJobSchedulerHostedService(IEnumerable<ICronJob> jobs, ILogger<CronJobSchedulerHostedService> logger)
    {
        _jobs = jobs;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<(ICronJob job, CronExpression cron)> schedules = new();
        foreach (var job in _jobs)
            try
            {
                var expr = CronExpression.Parse(job.CronExpression, CronFormat.Standard);
                schedules.Add((job, expr));
                _logger.LogInformation("Cron job registered: {Name} ({Cron})", job.Name, job.CronExpression);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Skipping job {Name}: invalid cron {Cron}", job.Name, job.CronExpression);
            }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var utcNow = DateTimeOffset.UtcNow;
            foreach (var (job, cron) in schedules)
            {
                var from = _lastRunUtc.TryGetValue(job.Name, out var last) ? last : DateTimeOffset.MinValue;
                var next = cron.GetNextOccurrence(from, TimeZoneInfo.Utc);
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