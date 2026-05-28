using Application.Contracts.Scheduling;
using Application.Contracts.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Worker.Settings;

public sealed class SystemSettingsBackupJob : ICronJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerSchedulingOptions _options;
    private readonly ILogger<SystemSettingsBackupJob> _logger;

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
        using IServiceScope scope = _scopeFactory.CreateScope();
        var appSettings = scope.ServiceProvider.GetRequiredService<IAppSettingsService>();
        var backupService = scope.ServiceProvider.GetRequiredService<ISystemSettingsBackupService>();

        bool enabled = await appSettings.GetBooleanAsync(
            Application.Modules.SystemSettings.AppSettingKeys.SystemBackupEnabled,
            defaultValue: false,
            stoppingToken);
        if (!enabled)
        {
            _logger.LogDebug("SystemSettingsBackupJob disabled by admin setting; skipping tick.");
            return;
        }

        string? retentionRaw = await appSettings.GetValueAsync(
            Application.Modules.SystemSettings.AppSettingKeys.SystemBackupRetentionDays,
            stoppingToken);
        int retentionDays = int.TryParse(retentionRaw, out int parsed) ? Math.Max(1, parsed) : 7;

        CreateSystemSettingsBackupResult created = await backupService.CreateBackupAsync("worker.cron", stoppingToken);
        int deleted = await backupService.CleanupExpiredBackupsAsync(retentionDays, stoppingToken);

        _logger.LogInformation(
            "System settings backup created ({Key}, {Bytes} bytes). Retention={RetentionDays}d deleted={Deleted}",
            created.Key,
            created.ByteSize,
            retentionDays,
            deleted);
    }
}
