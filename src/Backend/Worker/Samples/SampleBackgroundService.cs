namespace Worker.Samples;

/// <summary>Template background worker: replace with Hangfire/Quartz, queue consumers, or outbox dispatch.</summary>
public sealed class SampleBackgroundService : BackgroundService
{
    private readonly ILogger<SampleBackgroundService> _logger;

    public SampleBackgroundService(ILogger<SampleBackgroundService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sample worker started. Add your recurring jobs or queue consumers here.");
        while (!stoppingToken.IsCancellationRequested) await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
    }
}