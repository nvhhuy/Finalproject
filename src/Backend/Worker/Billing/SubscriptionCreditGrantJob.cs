using Application.Contracts.Scheduling;
using Application.Modules.Billing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Worker.Billing;

public sealed class SubscriptionCreditGrantJob : ICronJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerSchedulingOptions _options;
    private readonly ILogger<SubscriptionCreditGrantJob> _logger;

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
        using IServiceScope scope = _scopeFactory.CreateScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Shared.Common.Result<int, Shared.Errors.Error> result =
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
