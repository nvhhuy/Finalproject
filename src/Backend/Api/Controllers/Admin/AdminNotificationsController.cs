using Api.Authorization;
using Api.Controllers.Base;
using Api.Filters;
using Api.Models;
using Api.Models.Admin;
using Application.Modules.Notifications.Admin;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;

namespace Api.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v{version:apiVersion}/admin/notifications")]
public sealed class AdminNotificationsController : ApiControllerBase
{
    public AdminNotificationsController(ISender sender) : base(sender) { }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// Sends an in-app announcement to every active member account (role User, not locked or deleted).
    /// Live SignalR delivery is retried by the worker for any rows not pushed immediately.
    /// </summary>
    [HttpPost("broadcast")]
    [RequiresIdempotencyKey(Scope = "admin.notifications.broadcast")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<BroadcastMemberNotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Broadcast(
        [FromBody] BroadcastMemberNotificationRequest body,
        CancellationToken cancellationToken = default)
    {
        Result<BroadcastMemberNotificationResult, Error> result = await Sender.Send(
            new BroadcastMemberNotificationCommand(
                CurrentAdminId,
                body.Title,
                body.Body,
                body.LinkUrl,
                ClientIp),
            cancellationToken);

        return MatchApi(
            result,
            value => OkApi(new BroadcastMemberNotificationResponse(value.RecipientCount)));
    }
}
