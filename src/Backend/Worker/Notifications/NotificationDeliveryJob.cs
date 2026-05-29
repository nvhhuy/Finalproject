using Application.Contracts.Scheduling;
using Application.Modules.Notifications;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Worker.Notifications;

public sealed class NotificationDeliveryJob : ICronJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerSchedulingOptions _options;
    private readonly ILogger<NotificationDeliveryJob> _logger;

    public NotificationDeliveryJob(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerSchedulingOptions> options,
        ILogger<NotificationDeliveryJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => nameof(NotificationDeliveryJob);
    public string CronExpression => _options.NotificationDeliveryCronExpression;

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Shared.Common.Result<int, Shared.Errors.Error> result = await sender.Send(
            new ProcessPendingNotificationDeliveriesCommand(_options.NotificationDeliveryBatchSize),
            stoppingToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Notification delivery tick failed: {Message}", result.Error.ErrorMessage);
            return;
        }

        if (result.Value > 0)
            _logger.LogInformation("Delivered {Count} pending live notification(s).", result.Value);
    }
}
