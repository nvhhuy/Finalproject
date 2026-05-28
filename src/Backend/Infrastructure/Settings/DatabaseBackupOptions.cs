namespace Infrastructure.Settings;

public sealed class DatabaseBackupOptions
{
    public const string SectionName = "DatabaseBackup";

    public string PgDumpPath { get; init; } = "pg_dump";
    public string PgRestorePath { get; init; } = "pg_restore";
}
