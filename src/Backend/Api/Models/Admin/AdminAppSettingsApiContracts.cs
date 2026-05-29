namespace Api.Models.Admin;

public sealed record SetBooleanSettingRequest(bool Value);

public sealed record BooleanSettingDto(string Key, bool Value);

public sealed record SystemBackupSettingsDto(bool Enabled, int RetentionDays);

public sealed record SetSystemBackupSettingsRequest(bool Enabled, int RetentionDays);

public sealed record RunSystemBackupResponse(string Key, DateTime CreatedAtUtc, long ByteSize);

public sealed record RestoreSystemBackupRequest(string Key, string Confirmation);

public sealed record SystemBackupItemDto(string Key, DateTime CreatedAtUtc, long ByteSize);

public sealed record ImportSystemBackupRestoreResponse(
    string Key,
    DateTime CreatedAtUtc,
    long ByteSize,
    int SettingsApplied);

public sealed record DatabaseBackupSettingsDto(bool Enabled, int RetentionDays);

public sealed record SetDatabaseBackupSettingsRequest(bool Enabled, int RetentionDays);

public sealed record RunDatabaseBackupResponse(string Key, DateTime CreatedAtUtc, long ByteSize);

public sealed record RestoreDatabaseBackupRequest(string Key, string Confirmation);

public sealed record DatabaseBackupItemDto(string Key, DateTime CreatedAtUtc, long ByteSize);

public sealed record ImportDatabaseBackupRestoreResponse(string Key, DateTime CreatedAtUtc, long ByteSize);