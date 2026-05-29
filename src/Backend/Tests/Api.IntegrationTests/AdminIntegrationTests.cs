using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Api.IntegrationTests.Support;
using Api.Models.Admin;
using Api.Models.Auth;
using Application.Modules.Users.Administration.Queries;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AdminIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public AdminIntegrationTests(IntegrationAppFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Admin_login_lists_users_and_reads_dashboard_metrics()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var admin = await app.LoginAdminAsync();
        using var adminClient = app.Client.WithBearer(admin.AccessToken);

        var listResponse = await adminClient.GetAsync("/api/v1/admin/users?page=1&pageSize=5");
        var list = await listResponse.ReadApiAsync<AdminUserListDto>();
        Assert.NotNull(list?.Data);
        Assert.True(list.Data.TotalCount >= 1);

        var metricsResponse = await adminClient.GetAsync("/api/v1/admin/metrics/dashboard");
        var metrics =
            await metricsResponse.ReadApiAsync<AdminDashboardMetricsDto>();
        Assert.NotNull(metrics?.Data);
        Assert.True(metrics.Data.TotalActiveUsers >= 1);
    }

    [SkippableFact]
    public async Task Admin_create_update_lock_unlock_and_soft_delete_user()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var admin = await app.LoginAdminAsync();
        using var adminClient = app.Client.WithBearer(admin.AccessToken);

        var email = $"managed-{Guid.NewGuid():N}@test.local";
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/users")
        {
            Content = JsonContent.Create(new AdminCreateUserRequest(
                "Managed",
                "User",
                $"mu{Guid.NewGuid():N}"[..10],
                email,
                IntegrationTestApp.DefaultPassword))
        }.WithIdempotencyKey();

        var createResponse = await adminClient.SendAsync(create);
        var created = await createResponse.ReadApiAsync<Guid>();
        var userId = created!.Data!;

        var detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);

        using var update = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/users/{userId}")
        {
            Content = JsonContent.Create(new AdminUpdateUserRequest(
                "Managed",
                "Updated",
                detail.UserName,
                email,
                "User",
                detail.RowVersion))
        }.WithIdempotencyKey();
        (await adminClient.SendAsync(update)).EnsureSuccessStatusCode();

        detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);

        using var lockUser = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/lock")
        {
            Content = JsonContent.Create(new LockUserRequest("integration test", detail.RowVersion))
        }.WithIdempotencyKey();
        (await adminClient.SendAsync(lockUser)).EnsureSuccessStatusCode();

        detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);
        Assert.True(detail.IsLocked);

        using var unlock = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/unlock")
        {
            Content = JsonContent.Create(new { rowVersion = detail.RowVersion })
        }.WithIdempotencyKey();
        (await adminClient.SendAsync(unlock)).EnsureSuccessStatusCode();

        detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);

        using var delete =
            new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/users/{userId}?rowVersion={detail.RowVersion}")
                .WithIdempotencyKey();
        var deleteResponse = await adminClient.SendAsync(delete);
        deleteResponse.EnsureSuccessStatusCode();

        var getDeleted = await adminClient.GetAsync($"/api/v1/admin/users/{userId}");
        Assert.Equal(HttpStatusCode.NotFound, getDeleted.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_mark_email_verified_resend_reset_password_and_revoke_sessions()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var email = $"adminops-{Guid.NewGuid():N}@test.local";
        using var register = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new
            {
                firstName = "Ops",
                lastName = "User",
                userName = $"op{Guid.NewGuid():N}"[..10],
                email,
                password = IntegrationTestApp.DefaultPassword
            })
        }.WithIdempotencyKey();
        await app.Client.SendAsync(register);

        var admin = await app.LoginAdminAsync();
        using var adminClient = app.Client.WithBearer(admin.AccessToken);

        var listResponse = await adminClient.GetAsync($"/api/v1/admin/users?search={Uri.EscapeDataString(email)}");
        var list = await listResponse.ReadApiAsync<AdminUserListDto>();
        var userId = list!.Data!.Items[0].Id;

        var detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);

        using var resend =
            new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/email/resend-verification")
            {
                Content = JsonContent.Create(new { rowVersion = detail.RowVersion })
            };
        (await adminClient.SendAsync(resend)).EnsureSuccessStatusCode();

        detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);

        using var verify = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/email/verify")
        {
            Content = JsonContent.Create(new { rowVersion = detail.RowVersion })
        };
        (await adminClient.SendAsync(verify)).EnsureSuccessStatusCode();

        detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);
        Assert.NotNull(detail.EmailVerifiedAtUtc);

        const string newPassword = "AdminSetPass789!";
        using var resetPwd = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/password/reset")
        {
            Content = JsonContent.Create(new AdminResetPasswordRequest(newPassword, detail.RowVersion))
        }.WithIdempotencyKey();
        (await adminClient.SendAsync(resetPwd)).EnsureSuccessStatusCode();

        detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);

        using var revoke = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/sessions/revoke")
        {
            Content = JsonContent.Create(new { rowVersion = detail.RowVersion })
        };
        (await adminClient.SendAsync(revoke)).EnsureSuccessStatusCode();

        var userAuth = await app.LoginAsync(email, newPassword);
        Assert.Equal(userId, userAuth.UserId);
    }

    [SkippableFact]
    public async Task Admin_impersonate_non_admin_user_issues_tokens()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var (target, _) = await app.RegisterVerifyAndLoginAsync();
        var admin = await app.LoginAdminAsync();
        using var adminClient = app.Client.WithBearer(admin.AccessToken);

        var detail = await app.GetAdminUserDetailAsync(target.UserId, admin.AccessToken);

        using var impersonate =
            new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{target.UserId}/impersonate")
            {
                Content = JsonContent.Create(new { rowVersion = detail.RowVersion })
            }.WithIdempotencyKey();

        var response = await adminClient.SendAsync(impersonate);
        var tokens = await response.ReadApiAsync<AuthResponseDto>();
        Assert.Equal(target.UserId, tokens!.Data!.UserId);
        Assert.Equal(target.Email, tokens.Data.Email);
    }

    [SkippableFact]
    public async Task Admin_settings_audit_logs_and_dlq_when_rabbit_disabled()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var admin = await app.LoginAdminAsync();
        using var adminClient = app.Client.WithBearer(admin.AccessToken);

        var getSetting = await adminClient.GetAsync("/api/v1/admin/settings/allow-public-register");
        getSetting.EnsureSuccessStatusCode();

        var putSetting = await adminClient.PutAsJsonAsync(
            "/api/v1/admin/settings/allow-public-register",
            new { value = true });
        putSetting.EnsureSuccessStatusCode();

        var auditResponse = await adminClient.GetAsync("/api/v1/admin/audit-logs?page=1&pageSize=10");
        auditResponse.EnsureSuccessStatusCode();

        var auditExport = await adminClient.GetAsync("/api/v1/admin/audit-logs/export?maxRows=100");
        auditExport.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", auditExport.Content.Headers.ContentType?.MediaType);
        var csv = await auditExport.Content.ReadAsStringAsync();
        Assert.StartsWith("id,createdAtUtc,action,actorUserId", csv, StringComparison.Ordinal);

        var dlqPeek = await adminClient.GetAsync("/api/v1/admin/messaging/dlq/media.uploaded.v1/peek?count=1");
        Assert.True(
            dlqPeek.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadRequest,
            $"Unexpected DLQ status: {dlqPeek.StatusCode}");
    }

    [SkippableFact]
    public async Task Admin_can_configure_system_and_database_backup_settings()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var admin = await app.LoginAdminAsync();
        using var adminClient = app.Client.WithBearer(admin.AccessToken);

        var getSystemBackup = await adminClient.GetAsync("/api/v1/admin/settings/system-backup");
        getSystemBackup.EnsureSuccessStatusCode();
        var systemBackupBefore =
            await getSystemBackup.ReadApiAsync<SystemBackupSettingsDto>();
        Assert.NotNull(systemBackupBefore?.Data);

        var putSystemBackup = await adminClient.PutAsJsonAsync(
            "/api/v1/admin/settings/system-backup",
            new SetSystemBackupSettingsRequest(true, 9));
        putSystemBackup.EnsureSuccessStatusCode();
        var systemBackupAfter =
            await putSystemBackup.ReadApiAsync<SystemBackupSettingsDto>();
        Assert.NotNull(systemBackupAfter?.Data);
        Assert.True(systemBackupAfter.Data.Enabled);
        Assert.Equal(9, systemBackupAfter.Data.RetentionDays);

        var getDatabaseBackup = await adminClient.GetAsync("/api/v1/admin/settings/database-backup");
        getDatabaseBackup.EnsureSuccessStatusCode();
        var databaseBackupBefore =
            await getDatabaseBackup.ReadApiAsync<DatabaseBackupSettingsDto>();
        Assert.NotNull(databaseBackupBefore?.Data);

        var putDatabaseBackup = await adminClient.PutAsJsonAsync(
            "/api/v1/admin/settings/database-backup",
            new SetDatabaseBackupSettingsRequest(true, 5));
        putDatabaseBackup.EnsureSuccessStatusCode();
        var databaseBackupAfter =
            await putDatabaseBackup.ReadApiAsync<DatabaseBackupSettingsDto>();
        Assert.NotNull(databaseBackupAfter?.Data);
        Assert.True(databaseBackupAfter.Data.Enabled);
        Assert.Equal(5, databaseBackupAfter.Data.RetentionDays);
    }

    [SkippableFact]
    public async Task Admin_can_import_restore_system_settings_backup()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        var app = IntegrationTestApp.FromFixture(_fixture);

        var admin = await app.LoginAdminAsync();
        using var adminClient = app.Client.WithBearer(admin.AccessToken);

        const string json = """
                            {
                              "version": 1,
                              "triggerSource": "legacy.migration",
                              "createdAtUtc": "2026-01-01T00:00:00Z",
                              "items": [
                                { "key": "feature.admin.system_backup.enabled", "value": "true" },
                                { "key": "feature.admin.system_backup.retention_days", "value": "11" }
                              ]
                            }
                            """;

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("IMPORT_RESTORE_SETTINGS"), "confirmation");
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(json))
        {
            Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
        }, "file", "legacy-settings.json");

        var response = await adminClient.PostAsync(
            "/api/v1/admin/settings/system-backup/import-restore",
            form);
        response.EnsureSuccessStatusCode();
        var imported =
            await response.ReadApiAsync<ImportSystemBackupRestoreResponse>();
        Assert.NotNull(imported?.Data);
        Assert.Equal(2, imported.Data.SettingsApplied);
        Assert.Contains("imported/", imported.Data.Key, StringComparison.Ordinal);

        var getSystemBackup = await adminClient.GetAsync("/api/v1/admin/settings/system-backup");
        getSystemBackup.EnsureSuccessStatusCode();
        var settings = await getSystemBackup.ReadApiAsync<SystemBackupSettingsDto>();
        Assert.NotNull(settings?.Data);
        Assert.True(settings.Data.Enabled);
        Assert.Equal(11, settings.Data.RetentionDays);

        var download = await adminClient.GetAsync(
            $"/api/v1/admin/settings/system-backup/download?key={Uri.EscapeDataString(imported.Data.Key)}");
        download.EnsureSuccessStatusCode();
        var bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
        Assert.Contains("application/json", download.Content.Headers.ContentType?.MediaType ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }
}