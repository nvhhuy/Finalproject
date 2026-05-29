using Application.Contracts.Scheduling;
using Application.Modules.Billing;
using MediatR;
using Microsoft.Extensions.Options;

namespace Worker.Billing;

public sealed class SubscriptionCreditGrantJob : ICronJob
{
    private readonly ILogger<SubscriptionCreditGrantJob> _logger;
    private readonly WorkerSchedulingOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;

    public SubscriptionCreditGrantJob(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerSchedulingOptions> options,
        ILogger<SubscriptionCreditGrantJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => nameof(SubscriptionCreditGrantJob);
    public string CronExpression => _options.SubscriptionCreditGrantCronExpression;

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result =
            await sender.Send(new ProcessSubscriptionCreditGrantsCommand(), stoppingToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Subscription credit grant tick failed: {Message}", result.Error.ErrorMessage);
            return;
        }

        if (result.Value > 0)
            _logger.LogInformation("Granted monthly plan credits for {Count} subscription(s).", result.Value);
    }
}