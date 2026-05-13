namespace Application.Contracts.Scheduling;

/// <summary>Register recurring work (cron syntax). Host resolves all implementations from DI.</summary>
public interface ICronJob
{
    string Name { get; }
    /// <summary>Standard five-field cron (minute hour day month day-of-week), e.g. <c>*/10 * * * *</c>.</summary>
    string CronExpression { get; }
    Task ExecuteAsync(CancellationToken stoppingToken);
}
