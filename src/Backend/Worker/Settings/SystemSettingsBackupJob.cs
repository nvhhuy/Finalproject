using Application.Contracts.Scheduling;
using Application.Contracts.Settings;
using Application.Modules.SystemSettings;
using Microsoft.Extensions.Options;

namespace Worker.Settings;

public sealed class SystemSettingsBackupJob : ICronJob
{
    private readonly ILogger<SystemSettingsBackupJob> _logger;
    private readonly WorkerSchedulingOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;

    public SystemSettingsBackupJob(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerSchedulingOptions> options,
        ILogger<SystemSettingsBackupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => nameof(SystemSettingsBackupJob);
    public string CronExpression => _options.SystemSettingsBackupCronExpression;

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var appSettings = scope.ServiceProvider.GetRequiredService<IAppSettingsService>();
        var backupService = scope.ServiceProvider.GetRequiredService<ISystemSettingsBackupService>();

        var enabled = await appSettings.GetBooleanAsync(
            AppSettingKeys.SystemBackupEnabled,
            false,
            stoppingToken);
        if (!enabled)
        {
            _logger.LogDebug("SystemSettingsBackupJob disabled by admin setting; skipping tick.");
            return;
        }

        var retentionRaw = await appSettings.GetValueAsync(
            AppSettingKeys.SystemBackupRetentionDays,
            stoppingToken);
        var retentionDays = int.TryParse(retentionRaw, out var parsed) ? Math.Max(1, parsed) : 7;

        var created = await backupService.CreateBackupAsync("worker.cron", stoppingToken);
        var deleted = await backupService.CleanupExpiredBackupsAsync(retentionDays, stoppingToken);

        _logger.LogInformation(
            "System settings backup created ({Key}, {Bytes} bytes). Retention={RetentionDays}d deleted={Deleted}",
            created.Key,
            created.ByteSize,
            retentionDays,
            deleted);
    }
}