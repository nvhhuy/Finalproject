namespace Infrastructure.Settings;

public sealed class DatabaseBackupOptions
{
    public const string SectionName = "DatabaseBackup";

    public string PgDumpPath { get; init; } = "pg_dump";
    public string PgRestorePath { get; init; } = "pg_restore";

    /// <summary>Max size for imported settings JSON snapshots.</summary>
    public long MaxSettingsImportBytes { get; init; } = 10 * 1024 * 1024;

    /// <summary>Max size for imported PostgreSQL custom-format dumps.</summary>
    public long MaxDatabaseImportBytes { get; init; } = 512L * 1024 * 1024;
}
