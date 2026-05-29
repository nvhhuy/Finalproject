using Api.Authorization;
using Api.Controllers.Base;
using Api.Models;
using Api.Models.Admin;
using Application.Contracts.Settings;
using Application.Modules.SystemSettings;
using Application.Modules.SystemSettings.Admin.Commands;
using Application.Modules.SystemSettings.Admin.Queries;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Api.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v{version:apiVersion}/admin/settings")]
public sealed class AdminSettingsController : ApiControllerBase
{
    private const int DefaultBackupRetentionDays = 7;
    private const string SettingsRestoreConfirmation = "RESTORE_SETTINGS";
    private const string DatabaseRestoreConfirmation = "RESTORE_DATABASE";
    private const string SettingsImportRestoreConfirmation = "IMPORT_RESTORE_SETTINGS";
    private const string DatabaseImportRestoreConfirmation = "IMPORT_RESTORE_DATABASE";
    private const long MaxSettingsImportBytes = 10 * 1024 * 1024;
    private const long MaxDatabaseImportBytes = 512L * 1024 * 1024;
    private readonly ISystemSettingsBackupService _systemSettingsBackupService;
    private readonly ISystemDatabaseBackupService _systemDatabaseBackupService;

    public AdminSettingsController(
        ISender sender,
        ISystemSettingsBackupService systemSettingsBackupService,
        ISystemDatabaseBackupService systemDatabaseBackupService) : base(sender)
    {
        _systemSettingsBackupService = systemSettingsBackupService;
        _systemDatabaseBackupService = systemDatabaseBackupService;
    }

    [HttpGet("allow-public-register")]
    [ProducesResponseType(typeof(ApiResponse<BooleanSettingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllowPublicRegister(CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(
            new GetBooleanAppSettingQuery(AppSettingKeys.AllowPublicRegister, DefaultValue: true),
            cancellationToken);
        return MatchApi(result, value => OkApi(new BooleanSettingDto(AppSettingKeys.AllowPublicRegister, value)));
    }

    [HttpPut("allow-public-register")]
    [ProducesResponseType(typeof(ApiResponse<BooleanSettingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetAllowPublicRegister([FromBody] SetBooleanSettingRequest body, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(
            new SetBooleanAppSettingCommand(AppSettingKeys.AllowPublicRegister, body.Value),
            cancellationToken);
        return MatchApi(result, _ => OkApi(new BooleanSettingDto(AppSettingKeys.AllowPublicRegister, body.Value)));
    }

    [HttpGet("system-backup")]
    [ProducesResponseType(typeof(ApiResponse<SystemBackupSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemBackupSettings(CancellationToken cancellationToken)
    {
        Result<bool, Error> enabledResult = await Sender.Send(
            new GetBooleanAppSettingQuery(AppSettingKeys.SystemBackupEnabled, DefaultValue: false),
            cancellationToken);
        if (enabledResult.IsFailure)
            return MatchApi(enabledResult, _ => Ok());

        Result<string, Error> retentionResult = await Sender.Send(
            new GetStringAppSettingQuery(AppSettingKeys.SystemBackupRetentionDays, DefaultValue: DefaultBackupRetentionDays.ToString()),
            cancellationToken);
        if (retentionResult.IsFailure)
            return MatchApi(retentionResult, _ => Ok());

        int retentionDays = int.TryParse(retentionResult.Value, out int parsed) ? Math.Max(1, parsed) : DefaultBackupRetentionDays;
        return OkApi(new SystemBackupSettingsDto(enabledResult.Value, retentionDays));
    }

    [HttpPut("system-backup")]
    [ProducesResponseType(typeof(ApiResponse<SystemBackupSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetSystemBackupSettings([FromBody] SetSystemBackupSettingsRequest body, CancellationToken cancellationToken)
    {
        int retentionDays = Math.Max(1, body.RetentionDays);
        Result<bool, Error> enabledResult = await Sender.Send(
            new SetBooleanAppSettingCommand(AppSettingKeys.SystemBackupEnabled, body.Enabled),
            cancellationToken);
        if (enabledResult.IsFailure)
            return MatchApi(enabledResult, _ => Ok());

        Result<bool, Error> retentionResult = await Sender.Send(
            new SetStringAppSettingCommand(AppSettingKeys.SystemBackupRetentionDays, retentionDays.ToString()),
            cancellationToken);
        if (retentionResult.IsFailure)
            return MatchApi(retentionResult, _ => Ok());

        return OkApi(new SystemBackupSettingsDto(body.Enabled, retentionDays));
    }

    [HttpPost("system-backup/run")]
    [ProducesResponseType(typeof(ApiResponse<RunSystemBackupResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunSystemBackup(CancellationToken cancellationToken)
    {
        CreateSystemSettingsBackupResult created = await _systemSettingsBackupService.CreateBackupAsync("admin.manual", cancellationToken);
        return OkApi(new RunSystemBackupResponse(created.Key, created.CreatedAtUtc, created.ByteSize));
    }

    [HttpGet("system-backup/history")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SystemBackupItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemBackupHistory([FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SystemSettingsBackupItem> items = await _systemSettingsBackupService.ListBackupsAsync(take, cancellationToken);
        IReadOnlyList<SystemBackupItemDto> dtos = items
            .Select(x => new SystemBackupItemDto(x.Key, x.CreatedAtUtc, x.ByteSize))
            .ToList();
        return OkApi(dtos);
    }

    [HttpGet("system-backup/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadSystemBackup([FromQuery] string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
            return MatchApi((Result<bool, Error>)new ValidationError("key", "Backup key is required."), _ => Ok());

        SystemBackupDownloadResult? file = await _systemSettingsBackupService.DownloadBackupAsync(key, cancellationToken);
        if (file is null)
            return MatchApi((Result<bool, Error>)new ValidationError("key", "Backup key is invalid or unreadable."), _ => Ok());

        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("system-backup/restore")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RestoreSystemBackup([FromBody] RestoreSystemBackupRequest body, CancellationToken cancellationToken)
    {
        if (!string.Equals(body.Confirmation, SettingsRestoreConfirmation, StringComparison.Ordinal))
            return MatchApi((Result<bool, Error>)new ValidationError("confirmation", $"Confirmation must equal '{SettingsRestoreConfirmation}'."), _ => Ok());

        bool restored = await _systemSettingsBackupService.RestoreBackupAsync(body.Key, cancellationToken);
        if (!restored)
            return MatchApi((Result<bool, Error>)new ValidationError("key", "Backup key is invalid or unreadable."), _ => Ok());

        return OkApi(true);
    }

    [HttpPost("system-backup/import-restore")]
    [RequestSizeLimit(MaxSettingsImportBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxSettingsImportBytes)]
    [ProducesResponseType(typeof(ApiResponse<ImportSystemBackupRestoreResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportRestoreSystemBackup(
        IFormFile? file,
        [FromForm] string confirmation,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmation, SettingsImportRestoreConfirmation, StringComparison.Ordinal))
        {
            return MatchApi(
                (Result<bool, Error>)new ValidationError(
                    "confirmation",
                    $"Confirmation must equal '{SettingsImportRestoreConfirmation}'."),
                _ => Ok());
        }

        if (file is null || file.Length == 0)
            return MatchApi((Result<bool, Error>)new ValidationError("file", "Backup file is required."), _ => Ok());

        if (file.Length > MaxSettingsImportBytes)
        {
            return MatchApi(
                (Result<bool, Error>)new ValidationError("file", $"Backup file must be at most {MaxSettingsImportBytes} bytes."),
                _ => Ok());
        }

        await using Stream stream = file.OpenReadStream();
        ImportSystemSettingsBackupResult? imported =
            await _systemSettingsBackupService.ImportAndRestoreBackupAsync(stream, cancellationToken);
        if (imported is null)
        {
            return MatchApi(
                (Result<bool, Error>)new ValidationError(
                    "file",
                    "Settings backup file is invalid. Use platform JSON export or a JSON array of { key, value } entries."),
                _ => Ok());
        }

        return OkApi(new ImportSystemBackupRestoreResponse(
            imported.Key,
            imported.CreatedAtUtc,
            imported.ByteSize,
            imported.SettingsApplied));
    }

    [HttpGet("database-backup")]
    [ProducesResponseType(typeof(ApiResponse<DatabaseBackupSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDatabaseBackupSettings(CancellationToken cancellationToken)
    {
        Result<bool, Error> enabledResult = await Sender.Send(
            new GetBooleanAppSettingQuery(AppSettingKeys.DatabaseBackupEnabled, DefaultValue: false),
            cancellationToken);
        if (enabledResult.IsFailure)
            return MatchApi(enabledResult, _ => Ok());

        Result<string, Error> retentionResult = await Sender.Send(
            new GetStringAppSettingQuery(AppSettingKeys.DatabaseBackupRetentionDays, DefaultValue: DefaultBackupRetentionDays.ToString()),
            cancellationToken);
        if (retentionResult.IsFailure)
            return MatchApi(retentionResult, _ => Ok());

        int retentionDays = int.TryParse(retentionResult.Value, out int parsed) ? Math.Max(1, parsed) : DefaultBackupRetentionDays;
        return OkApi(new DatabaseBackupSettingsDto(enabledResult.Value, retentionDays));
    }

    [HttpPut("database-backup")]
    [ProducesResponseType(typeof(ApiResponse<DatabaseBackupSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetDatabaseBackupSettings([FromBody] SetDatabaseBackupSettingsRequest body, CancellationToken cancellationToken)
    {
        int retentionDays = Math.Max(1, body.RetentionDays);
        Result<bool, Error> enabledResult = await Sender.Send(
            new SetBooleanAppSettingCommand(AppSettingKeys.DatabaseBackupEnabled, body.Enabled),
            cancellationToken);
        if (enabledResult.IsFailure)
            return MatchApi(enabledResult, _ => Ok());

        Result<bool, Error> retentionResult = await Sender.Send(
            new SetStringAppSettingCommand(AppSettingKeys.DatabaseBackupRetentionDays, retentionDays.ToString()),
            cancellationToken);
        if (retentionResult.IsFailure)
            return MatchApi(retentionResult, _ => Ok());

        return OkApi(new DatabaseBackupSettingsDto(body.Enabled, retentionDays));
    }

    [HttpPost("database-backup/run")]
    [ProducesResponseType(typeof(ApiResponse<RunDatabaseBackupResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunDatabaseBackup(CancellationToken cancellationToken)
    {
        CreateSystemDatabaseBackupResult created = await _systemDatabaseBackupService.CreateBackupAsync("admin.manual", cancellationToken);
        return OkApi(new RunDatabaseBackupResponse(created.Key, created.CreatedAtUtc, created.ByteSize));
    }

    [HttpGet("database-backup/history")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DatabaseBackupItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDatabaseBackupHistory([FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SystemDatabaseBackupItem> items = await _systemDatabaseBackupService.ListBackupsAsync(take, cancellationToken);
        IReadOnlyList<DatabaseBackupItemDto> dtos = items
            .Select(x => new DatabaseBackupItemDto(x.Key, x.CreatedAtUtc, x.ByteSize))
            .ToList();
        return OkApi(dtos);
    }

    [HttpGet("database-backup/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadDatabaseBackup([FromQuery] string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
            return MatchApi((Result<bool, Error>)new ValidationError("key", "Database backup key is required."), _ => Ok());

        SystemDatabaseBackupDownloadResult? file = await _systemDatabaseBackupService.DownloadBackupAsync(key, cancellationToken);
        if (file is null)
            return MatchApi((Result<bool, Error>)new ValidationError("key", "Database backup key is invalid or unreadable."), _ => Ok());

        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("database-backup/restore")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RestoreDatabaseBackup([FromBody] RestoreDatabaseBackupRequest body, CancellationToken cancellationToken)
    {
        if (!string.Equals(body.Confirmation, DatabaseRestoreConfirmation, StringComparison.Ordinal))
            return MatchApi((Result<bool, Error>)new ValidationError("confirmation", $"Confirmation must equal '{DatabaseRestoreConfirmation}'."), _ => Ok());

        bool restored = await _systemDatabaseBackupService.RestoreBackupAsync(body.Key, cancellationToken);
        if (!restored)
            return MatchApi((Result<bool, Error>)new ValidationError("key", "Database backup key is invalid or restore failed."), _ => Ok());

        return OkApi(true);
    }

    [HttpPost("database-backup/import-restore")]
    [RequestSizeLimit(MaxDatabaseImportBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxDatabaseImportBytes)]
    [ProducesResponseType(typeof(ApiResponse<ImportDatabaseBackupRestoreResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportRestoreDatabaseBackup(
        IFormFile? file,
        [FromForm] string confirmation,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmation, DatabaseImportRestoreConfirmation, StringComparison.Ordinal))
        {
            return MatchApi(
                (Result<bool, Error>)new ValidationError(
                    "confirmation",
                    $"Confirmation must equal '{DatabaseImportRestoreConfirmation}'."),
                _ => Ok());
        }

        if (file is null || file.Length == 0)
            return MatchApi((Result<bool, Error>)new ValidationError("file", "Database dump file is required."), _ => Ok());

        if (file.Length > MaxDatabaseImportBytes)
        {
            return MatchApi(
                (Result<bool, Error>)new ValidationError("file", $"Database dump must be at most {MaxDatabaseImportBytes} bytes."),
                _ => Ok());
        }

        await using Stream stream = file.OpenReadStream();
        ImportSystemDatabaseBackupResult? imported =
            await _systemDatabaseBackupService.ImportAndRestoreBackupAsync(stream, cancellationToken);
        if (imported is null)
        {
            return MatchApi(
                (Result<bool, Error>)new ValidationError(
                    "file",
                    "Database dump is invalid or restore failed. Use PostgreSQL custom format (pg_dump -Fc) from the legacy system."),
                _ => Ok());
        }

        return OkApi(new ImportDatabaseBackupRestoreResponse(imported.Key, imported.CreatedAtUtc, imported.ByteSize));
    }
}
