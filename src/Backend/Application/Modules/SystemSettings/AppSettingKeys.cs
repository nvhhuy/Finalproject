namespace Application.Modules.SystemSettings;

public static class AppSettingKeys
{
    public const string AllowPublicRegister = "feature.auth.allow_public_register";
    public const string SystemBackupEnabled = "feature.admin.system_backup.enabled";
    public const string SystemBackupRetentionDays = "feature.admin.system_backup.retention_days";
    public const string DatabaseBackupEnabled = "feature.admin.database_backup.enabled";
    public const string DatabaseBackupRetentionDays = "feature.admin.database_backup.retention_days";
}