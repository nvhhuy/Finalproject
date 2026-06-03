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

namespace Api.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v{version:apiVersion}/admin/notifications")]
public sealed class AdminNotificationsController : ApiControllerBase
{
    public AdminNotificationsController(ISender sender) : base(sender)
    {
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    ///     Queues an in-app announcement for every active member (role User, not locked or deleted).
    ///     Fan-out runs asynchronously via outbox and worker; live SignalR/Web Push is retried by the worker.
    ///     Response <c>recipientCount</c> is the number of members that will receive the message.
    /// </summary>
    [HttpPost("broadcast")]
    [RequiresIdempotencyKey(Scope = "admin.notifications.broadcast")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<BroadcastMemberNotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Broadcast(
        [FromBody] BroadcastMemberNotificationRequest body,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
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