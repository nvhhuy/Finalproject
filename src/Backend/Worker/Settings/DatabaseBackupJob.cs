using Application.Contracts.Scheduling;
using Application.Contracts.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Worker.Settings;

public sealed class DatabaseBackupJob : ICronJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerSchedulingOptions _options;
    private readonly ILogger<DatabaseBackupJob> _logger;

    public DatabaseBackupJob(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerSchedulingOptions> options,
        ILogger<DatabaseBackupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => nameof(DatabaseBackupJob);
    public string CronExpression => _options.DatabaseBackupCronExpression;

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var appSettings = scope.ServiceProvider.GetRequiredService<IAppSettingsService>();
        var backupService = scope.ServiceProvider.GetRequiredService<ISystemDatabaseBackupService>();

        bool enabled = await appSettings.GetBooleanAsync(
            Application.Modules.SystemSettings.AppSettingKeys.DatabaseBackupEnabled,
            defaultValue: false,
            stoppingToken);
        if (!enabled)
        {
            _logger.LogDebug("DatabaseBackupJob disabled by admin setting; skipping tick.");
            return;
        }

        string? retentionRaw = await appSettings.GetValueAsync(
            Application.Modules.SystemSettings.AppSettingKeys.DatabaseBackupRetentionDays,
            stoppingToken);
        int retentionDays = int.TryParse(retentionRaw, out int parsed) ? Math.Max(1, parsed) : 7;

        CreateSystemDatabaseBackupResult created = await backupService.CreateBackupAsync("worker.cron", stoppingToken);
        int deleted = await backupService.CleanupExpiredBackupsAsync(retentionDays, stoppingToken);

        _logger.LogInformation(
            "Database backup created ({Key}, {Bytes} bytes). Retention={RetentionDays}d deleted={Deleted}",
            created.Key,
            created.ByteSize,
            retentionDays,
            deleted);
    }
}
