using System.Net;
using System.Net.Http.Json;
using Api.IntegrationTests.Support;
using Api.Models;
using Api.Models.Admin;
using Api.Models.Auth;
using Application.Modules.Users.Administration.Queries;

namespace Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AdminIntegrationTests
{
    private readonly IntegrationAppFixture _fixture;

    public AdminIntegrationTests(IntegrationAppFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task Admin_login_lists_users_and_reads_dashboard_metrics()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        AuthResponseDto admin = await app.LoginAdminAsync();
        using HttpClient adminClient = app.Client.WithBearer(admin.AccessToken);

        HttpResponseMessage listResponse = await adminClient.GetAsync("/api/v1/admin/users?page=1&pageSize=5");
        ApiResponse<AdminUserListDto>? list = await listResponse.ReadApiAsync<AdminUserListDto>();
        Assert.NotNull(list?.Data);
        Assert.True(list.Data.TotalCount >= 1);

        HttpResponseMessage metricsResponse = await adminClient.GetAsync("/api/v1/admin/metrics/dashboard");
        ApiResponse<AdminDashboardMetricsDto>? metrics =
            await metricsResponse.ReadApiAsync<AdminDashboardMetricsDto>();
        Assert.NotNull(metrics?.Data);
        Assert.True(metrics.Data.TotalActiveUsers >= 1);
    }

    [SkippableFact]
    public async Task Admin_create_update_lock_unlock_and_soft_delete_user()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        AuthResponseDto admin = await app.LoginAdminAsync();
        using HttpClient adminClient = app.Client.WithBearer(admin.AccessToken);

        string email = $"managed-{Guid.NewGuid():N}@test.local";
        using HttpRequestMessage create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/users")
        {
            Content = JsonContent.Create(new AdminCreateUserRequest(
                "Managed",
                "User",
                $"mu{Guid.NewGuid():N}"[..10],
                email,
                IntegrationTestApp.DefaultPassword,
                "User")),
        }.WithIdempotencyKey();

        HttpResponseMessage createResponse = await adminClient.SendAsync(create);
        ApiResponse<Guid>? created = await createResponse.ReadApiAsync<Guid>();
        Guid userId = created!.Data!;

        AdminUserDetailDto detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);

        using HttpRequestMessage update = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/users/{userId}")
        {
            Content = JsonContent.Create(new AdminUpdateUserRequest(
                "Managed",
                "Updated",
                detail.UserName,
                email,
                "User",
                detail.RowVersion)),
        }.WithIdempotencyKey();
        (await adminClient.SendAsync(update)).EnsureSuccessStatusCode();

        detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);

        using HttpRequestMessage lockUser = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/lock")
        {
            Content = JsonContent.Create(new LockUserRequest("integration test", detail.RowVersion)),
        }.WithIdempotencyKey();
        (await adminClient.SendAsync(lockUser)).EnsureSuccessStatusCode();

        detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);
        Assert.True(detail.IsLocked);

        using HttpRequestMessage unlock = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/unlock")
        {
            Content = JsonContent.Create(new { rowVersion = detail.RowVersion }),
        }.WithIdempotencyKey();
        (await adminClient.SendAsync(unlock)).EnsureSuccessStatusCode();

        detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);

        using HttpRequestMessage delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/users/{userId}?rowVersion={detail.RowVersion}")
            .WithIdempotencyKey();
        HttpResponseMessage deleteResponse = await adminClient.SendAsync(delete);
        deleteResponse.EnsureSuccessStatusCode();

        HttpResponseMessage getDeleted = await adminClient.GetAsync($"/api/v1/admin/users/{userId}");
        Assert.Equal(HttpStatusCode.NotFound, getDeleted.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_mark_email_verified_resend_reset_password_and_revoke_sessions()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        string email = $"adminops-{Guid.NewGuid():N}@test.local";
        using HttpRequestMessage register = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new
            {
                firstName = "Ops",
                lastName = "User",
                userName = $"op{Guid.NewGuid():N}"[..10],
                email,
                password = IntegrationTestApp.DefaultPassword,
            }),
        }.WithIdempotencyKey();
        await app.Client.SendAsync(register);

        AuthResponseDto admin = await app.LoginAdminAsync();
        using HttpClient adminClient = app.Client.WithBearer(admin.AccessToken);

        HttpResponseMessage listResponse = await adminClient.GetAsync($"/api/v1/admin/users?search={Uri.EscapeDataString(email)}");
        ApiResponse<AdminUserListDto>? list = await listResponse.ReadApiAsync<AdminUserListDto>();
        Guid userId = list!.Data!.Items[0].Id;

        AdminUserDetailDto detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);

        using HttpRequestMessage resend = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/email/resend-verification")
        {
            Content = JsonContent.Create(new { rowVersion = detail.RowVersion }),
        };
        (await adminClient.SendAsync(resend)).EnsureSuccessStatusCode();

        detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);

        using HttpRequestMessage verify = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/email/verify")
        {
            Content = JsonContent.Create(new { rowVersion = detail.RowVersion }),
        };
        (await adminClient.SendAsync(verify)).EnsureSuccessStatusCode();

        detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);
        Assert.NotNull(detail.EmailVerifiedAtUtc);

        const string newPassword = "AdminSetPass789!";
        using HttpRequestMessage resetPwd = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/password/reset")
        {
            Content = JsonContent.Create(new AdminResetPasswordRequest(newPassword, detail.RowVersion)),
        }.WithIdempotencyKey();
        (await adminClient.SendAsync(resetPwd)).EnsureSuccessStatusCode();

        detail = await app.GetAdminUserDetailAsync(userId, admin.AccessToken);

        using HttpRequestMessage revoke = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/sessions/revoke")
        {
            Content = JsonContent.Create(new { rowVersion = detail.RowVersion }),
        };
        (await adminClient.SendAsync(revoke)).EnsureSuccessStatusCode();

        AuthResponseDto userAuth = await app.LoginAsync(email, newPassword);
        Assert.Equal(userId, userAuth.UserId);
    }

    [SkippableFact]
    public async Task Admin_impersonate_non_admin_user_issues_tokens()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        (AuthResponseDto target, _) = await app.RegisterVerifyAndLoginAsync();
        AuthResponseDto admin = await app.LoginAdminAsync();
        using HttpClient adminClient = app.Client.WithBearer(admin.AccessToken);

        AdminUserDetailDto detail = await app.GetAdminUserDetailAsync(target.UserId, admin.AccessToken);

        using HttpRequestMessage impersonate = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{target.UserId}/impersonate")
        {
            Content = JsonContent.Create(new { rowVersion = detail.RowVersion }),
        }.WithIdempotencyKey();

        HttpResponseMessage response = await adminClient.SendAsync(impersonate);
        ApiResponse<AuthResponseDto>? tokens = await response.ReadApiAsync<AuthResponseDto>();
        Assert.Equal(target.UserId, tokens!.Data!.UserId);
        Assert.Equal(target.Email, tokens.Data.Email);
    }

    [SkippableFact]
    public async Task Admin_settings_audit_logs_and_dlq_when_rabbit_disabled()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        AuthResponseDto admin = await app.LoginAdminAsync();
        using HttpClient adminClient = app.Client.WithBearer(admin.AccessToken);

        HttpResponseMessage getSetting = await adminClient.GetAsync("/api/v1/admin/settings/allow-public-register");
        getSetting.EnsureSuccessStatusCode();

        HttpResponseMessage putSetting = await adminClient.PutAsJsonAsync(
            "/api/v1/admin/settings/allow-public-register",
            new { value = true });
        putSetting.EnsureSuccessStatusCode();

        HttpResponseMessage auditResponse = await adminClient.GetAsync("/api/v1/admin/audit-logs?page=1&pageSize=10");
        auditResponse.EnsureSuccessStatusCode();

        HttpResponseMessage auditExport = await adminClient.GetAsync("/api/v1/admin/audit-logs/export?maxRows=100");
        auditExport.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", auditExport.Content.Headers.ContentType?.MediaType);
        string csv = await auditExport.Content.ReadAsStringAsync();
        Assert.StartsWith("id,createdAtUtc,action,actorUserId", csv, StringComparison.Ordinal);

        HttpResponseMessage dlqPeek = await adminClient.GetAsync("/api/v1/admin/messaging/dlq/media.uploaded.v1/peek?count=1");
        Assert.True(
            dlqPeek.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadRequest,
            $"Unexpected DLQ status: {dlqPeek.StatusCode}");
    }

    [SkippableFact]
    public async Task Admin_can_configure_system_and_database_backup_settings()
    {
        Skip.IfNot(_fixture.Postgres.IsAvailable, PostgresFixture.SkipReason);
        IntegrationTestApp app = IntegrationTestApp.FromFixture(_fixture);

        AuthResponseDto admin = await app.LoginAdminAsync();
        using HttpClient adminClient = app.Client.WithBearer(admin.AccessToken);

        HttpResponseMessage getSystemBackup = await adminClient.GetAsync("/api/v1/admin/settings/system-backup");
        getSystemBackup.EnsureSuccessStatusCode();
        ApiResponse<SystemBackupSettingsDto>? systemBackupBefore =
            await getSystemBackup.ReadApiAsync<SystemBackupSettingsDto>();
        Assert.NotNull(systemBackupBefore?.Data);

        HttpResponseMessage putSystemBackup = await adminClient.PutAsJsonAsync(
            "/api/v1/admin/settings/system-backup",
            new SetSystemBackupSettingsRequest(Enabled: true, RetentionDays: 9));
        putSystemBackup.EnsureSuccessStatusCode();
        ApiResponse<SystemBackupSettingsDto>? systemBackupAfter =
            await putSystemBackup.ReadApiAsync<SystemBackupSettingsDto>();
        Assert.NotNull(systemBackupAfter?.Data);
        Assert.True(systemBackupAfter.Data.Enabled);
        Assert.Equal(9, systemBackupAfter.Data.RetentionDays);

        HttpResponseMessage getDatabaseBackup = await adminClient.GetAsync("/api/v1/admin/settings/database-backup");
        getDatabaseBackup.EnsureSuccessStatusCode();
        ApiResponse<DatabaseBackupSettingsDto>? databaseBackupBefore =
            await getDatabaseBackup.ReadApiAsync<DatabaseBackupSettingsDto>();
        Assert.NotNull(databaseBackupBefore?.Data);

        HttpResponseMessage putDatabaseBackup = await adminClient.PutAsJsonAsync(
            "/api/v1/admin/settings/database-backup",
            new SetDatabaseBackupSettingsRequest(Enabled: true, RetentionDays: 5));
        putDatabaseBackup.EnsureSuccessStatusCode();
        ApiResponse<DatabaseBackupSettingsDto>? databaseBackupAfter =
            await putDatabaseBackup.ReadApiAsync<DatabaseBackupSettingsDto>();
        Assert.NotNull(databaseBackupAfter?.Data);
        Assert.True(databaseBackupAfter.Data.Enabled);
        Assert.Equal(5, databaseBackupAfter.Data.RetentionDays);
    }
}
