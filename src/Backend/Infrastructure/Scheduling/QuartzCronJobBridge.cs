using Application.Contracts.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Infrastructure.Scheduling;

/// <summary>Runs a named <see cref="ICronJob" /> from DI (used by Quartz scheduler).</summary>
[DisallowConcurrentExecution]
public sealed class QuartzCronJobBridge : IJob
{
    public const string JobNameKey = "CronJobName";
    private readonly ILogger<QuartzCronJobBridge> _logger;

    private readonly IServiceScopeFactory _scopeFactory;

    public QuartzCronJobBridge(IServiceScopeFactory scopeFactory, ILogger<QuartzCronJobBridge> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var name = context.MergedJobDataMap.GetString(JobNameKey);
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Quartz job missing {Key}", JobNameKey);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var job = scope.ServiceProvider.GetServices<ICronJob>().FirstOrDefault(j => j.Name == name);
        if (job is null)
        {
            _logger.LogWarning("No ICronJob registered with name {Name}", name);
            return;
        }

        await job.ExecuteAsync(context.CancellationToken);
    }
}