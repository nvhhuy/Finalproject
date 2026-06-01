using Api.Controllers.Base;
using Api.Filters;
using Api.Models;
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
}