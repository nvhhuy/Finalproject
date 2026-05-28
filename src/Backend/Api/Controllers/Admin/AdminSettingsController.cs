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
}
