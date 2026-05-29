using Application.Contracts.Scheduling;
using Application.Modules.Billing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Worker.Billing;

public sealed class SubscriptionDunningJob : ICronJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerSchedulingOptions _options;
    private readonly ILogger<SubscriptionDunningJob> _logger;

    public SubscriptionDunningJob(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerSchedulingOptions> options,
        ILogger<SubscriptionDunningJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => nameof(SubscriptionDunningJob);
    public string CronExpression => _options.SubscriptionDunningCronExpression;

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Shared.Common.Result<int, Shared.Errors.Error> result =
            await sender.Send(new ProcessSubscriptionDunningCommand(), stoppingToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Subscription dunning tick failed: {Message}", result.Error.ErrorMessage);
            return;
        }

        if (result.Value > 0)
            _logger.LogInformation("Subscription dunning sent {Count} notification(s).", result.Value);
    }
}
