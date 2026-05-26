using Application.Contracts.Scheduling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Worker.Samples;

/// <summary>Example cron job; register more <see cref="ICronJob"/> singletons to extend scheduling.</summary>
public sealed class SampleCronJob : ICronJob
{
    private readonly ILogger<SampleCronJob> _logger;
    private readonly WorkerSchedulingOptions _options;

    public SampleCronJob(IOptions<WorkerSchedulingOptions> options, ILogger<SampleCronJob> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string Name => nameof(SampleCronJob);

    public string CronExpression => _options.SampleCronExpression;

    public Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sample cron tick at {Utc}", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}
