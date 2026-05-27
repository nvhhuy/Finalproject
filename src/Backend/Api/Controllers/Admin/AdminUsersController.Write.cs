using Api.Filters;
using Api.Models;
using Api.Models.Admin;
using Api.Models.Auth;
using Application.Modules.Users.Administration.Commands;
using Application.Modules.Users.Authentication.Common;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;

namespace Api.Controllers.Admin;

/// <summary>Admin commands (write database). Each mutating action runs in its own EF transaction.</summary>
public sealed partial class AdminUsersController
{
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost]
    [RequiresIdempotencyKey(Scope = "admin.users.create")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequest body, CancellationToken cancellationToken)
    {
        Result<Guid, Error> result = await Sender.Send(
            new AdminCreateUserCommand(
                CurrentAdminId,
                body.FirstName,
                body.LastName,
                body.UserName,
                body.Email,
                body.Password,
                body.Role,
                ClientIp),
            cancellationToken);
        return MatchApi(result);
    }

    [HttpPut("{userId:guid}")]
    [RequiresIdempotencyKey(Scope = "admin.users.update")]
    [Transactional]
    public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] AdminUpdateUserRequest body, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(
            new AdminUpdateUserCommand(
                CurrentAdminId,
                userId,
                body.FirstName,
                body.LastName,
                body.UserName,
                body.Email,
                body.Role,
                ClientIp),
            cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpDelete("{userId:guid}")]
    [RequiresIdempotencyKey(Scope = "admin.users.delete")]
    [Transactional]
    public async Task<IActionResult> DeleteUser(Guid userId, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new AdminDeleteUserCommand(CurrentAdminId, userId, ClientIp), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("{userId:guid}/lock")]
    [RequiresIdempotencyKey(Scope = "admin.users.lock")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LockUser(Guid userId, [FromBody] LockUserRequest? body, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new AdminLockUserCommand(userId, body?.Reason, CurrentAdminId, ClientIp), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("{userId:guid}/unlock")]
    [RequiresIdempotencyKey(Scope = "admin.users.unlock")]
    [Transactional]
    public async Task<IActionResult> UnlockUser(Guid userId, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new AdminUnlockUserCommand(userId, CurrentAdminId, ClientIp), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("{userId:guid}/impersonate")]
    [RequiresIdempotencyKey(Scope = "admin.users.impersonate")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ImpersonateUser(Guid userId, CancellationToken cancellationToken)
    {
        Result<AuthenticationResponse, Error> result =
            await Sender.Send(new ImpersonateUserCommand(userId, CurrentAdminId, ClientIp), cancellationToken);
        return MatchApi(result, r => OkApi(AuthResponseDto.From(r)));
    }

    [HttpPost("{userId:guid}/2fa/reset")]
    [RequiresIdempotencyKey(Scope = "admin.users.2fa.reset")]
    [Transactional]
    public async Task<IActionResult> ResetTwoFactor(Guid userId, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new AdminResetTwoFactorCommand(CurrentAdminId, userId, ClientIp), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("{userId:guid}/password/reset")]
    [RequiresIdempotencyKey(Scope = "admin.users.password.reset")]
    [Transactional]
    public async Task<IActionResult> ResetPassword(Guid userId, [FromBody] AdminResetPasswordRequest body, CancellationToken cancellationToken)
    {
        Result<bool, Error> result =
            await Sender.Send(new AdminResetPasswordCommand(CurrentAdminId, userId, body.NewPassword, ClientIp), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("{userId:guid}/email/verify")]
    [Transactional]
    public async Task<IActionResult> MarkEmailVerified(Guid userId, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new AdminMarkEmailVerifiedCommand(CurrentAdminId, userId, ClientIp), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("{userId:guid}/email/resend-verification")]
    [Transactional]
    public async Task<IActionResult> ResendEmailVerification(Guid userId, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new AdminResendEmailVerificationCommand(CurrentAdminId, userId, ClientIp), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("{userId:guid}/sessions/revoke")]
    [Transactional]
    public async Task<IActionResult> RevokeSessions(Guid userId, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new AdminRevokeUserSessionsCommand(CurrentAdminId, userId, ClientIp), cancellationToken);
        return MatchApiEmpty(result);
    }
}
