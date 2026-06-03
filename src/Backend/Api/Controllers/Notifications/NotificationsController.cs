using Api.Controllers.Base;
using Api.Filters;
using Api.Models;
using Api.Models.Notifications;
using Application.Modules.Notifications;
using Application.Modules.Notifications.Commands;
using Application.Modules.Notifications.Queries;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Notifications;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/notifications")]
public sealed class NotificationsController : ApiControllerBase
{
    public NotificationsController(ISender sender) : base(sender)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<NotificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new ListUserNotificationsQuery(CurrentUserId, page, pageSize),
            cancellationToken);
        return MatchApi(result, OkApi);
    }

    [HttpPost("{id:guid}/read")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new MarkNotificationReadCommand(CurrentUserId, id),
            cancellationToken);
        return MatchApi(result, _ => OkApi(true));
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetNotificationUnreadCountQuery(CurrentUserId), cancellationToken);
        return MatchApi(result, OkApi);
    }

    [HttpGet("push/vapid-public-key")]
    [ProducesResponseType(typeof(ApiResponse<string?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VapidPublicKey(CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetWebPushVapidPublicKeyQuery(), cancellationToken);
        return MatchApi(result, OkApi);
    }

    [HttpPost("push/subscribe")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubscribePush(
        [FromBody] RegisterPushSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new RegisterPushSubscriptionCommand(
                CurrentUserId,
                request.Endpoint,
                request.Keys.P256dh,
                request.Keys.Auth,
                Request.Headers.UserAgent.ToString()),
            cancellationToken);
        return MatchApi(result, _ => OkApi(true));
    }

    [HttpDelete("push/subscribe")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnsubscribePush(
        [FromBody] RemovePushSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new RemovePushSubscriptionCommand(CurrentUserId, request.Endpoint),
            cancellationToken);
        return MatchApi(result, _ => OkApi(true));
    }
}