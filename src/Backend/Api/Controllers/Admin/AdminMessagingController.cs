using Api.Authorization;
using Api.Controllers.Base;
using Api.Filters;
using Api.Models;
using Application.Contracts.Administration;
using Application.Contracts.Messaging;
using Application.Modules.Users.Administration;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Admin;

/// <summary>
///     Operator tooling for inspecting and triaging dead-letter queues. Every endpoint is
///     admin-gated — replaying a DLQ message can re-trigger side effects (emails, charges, media
///     processing) so this is firmly an operator-only surface.
/// </summary>
/// <remarks>
///     Typical incident flow:
///     <list type="number">
///         <item>On-call sees DLQ depth alert from monitoring.</item>
///         <item>Calls <c>GET /admin/messaging/dlq/{queue}/peek</c> to triage what's stuck.</item>
///         <item>Fixes the underlying bug (handler crash, missing config, etc.) and deploys.</item>
///         <item>
///             Calls <c>POST /admin/messaging/dlq/{queue}/replay</c> to feed the queued failures back into the primary
///             worker.
///         </item>
///         <item>
///             Drains residual unprocessable messages via <c>POST /admin/messaging/dlq/{queue}/drain</c> after a
///             deliberate review.
///         </item>
///     </list>
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v{version:apiVersion}/admin/messaging")]
public sealed class AdminMessagingController : ApiControllerBase
{
    private readonly IAdminAuditLogWriter _audit;
    private readonly IDeadLetterAdminService _dlqAdmin;

    public AdminMessagingController(ISender sender, IDeadLetterAdminService dlqAdmin, IAdminAuditLogWriter audit) :
        base(sender)
    {
        _dlqAdmin = dlqAdmin;
        _audit = audit;
    }

    /// <summary>
    ///     Fetches up to <paramref name="count" /> messages from a DLQ without consuming them.
    ///     Each peek returns the raw payload preview, original routing key, and the broker-recorded
    ///     retry count so operators can spot poison messages.
    /// </summary>
    [HttpGet("dlq/{queueName}/peek")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DeadLetterMessage>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Peek(
        [FromRoute] string queueName,
        [FromQuery] int count = 10,
        CancellationToken cancellationToken = default)
    {
        var messages = await _dlqAdmin.PeekAsync(queueName, count, cancellationToken);
        _audit.Append(
            CurrentAdminId,
            AdminAuditActions.MessagingDeadLetterPeeked,
            null,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"queue={queueName};count={count};returned={messages.Count}");
        return OkApi(messages);
    }

    /// <summary>
    ///     Pulls up to <paramref name="count" /> messages from a DLQ and republishes each to its
    ///     originating primary queue. Returns the count of successful replays — if it's less than
    ///     <paramref name="count" />, either the queue ran out or a republish failed (check logs).
    /// </summary>
    [HttpPost("dlq/{queueName}/replay")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<DeadLetterActionResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Replay(
        [FromRoute] string queueName,
        [FromQuery] int count = 10,
        CancellationToken cancellationToken = default)
    {
        var replayed = await _dlqAdmin.ReplayAsync(queueName, count, cancellationToken);
        _audit.Append(
            CurrentAdminId,
            AdminAuditActions.MessagingDeadLetterReplayed,
            null,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"queue={queueName};requested={count};replayed={replayed}");
        return OkApi(new DeadLetterActionResult(queueName, "replay", replayed));
    }

    /// <summary>
    ///     Pulls up to <paramref name="count" /> messages from a DLQ and acks them without
    ///     republishing — effectively discarding. Destructive; use only after deliberate review.
    /// </summary>
    [HttpPost("dlq/{queueName}/drain")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<DeadLetterActionResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Drain(
        [FromRoute] string queueName,
        [FromQuery] int count = 10,
        CancellationToken cancellationToken = default)
    {
        var drained = await _dlqAdmin.DrainAsync(queueName, count, cancellationToken);
        _audit.Append(
            CurrentAdminId,
            AdminAuditActions.MessagingDeadLetterDrained,
            null,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"queue={queueName};requested={count};drained={drained}");
        return OkApi(new DeadLetterActionResult(queueName, "drain", drained));
    }

    public sealed record DeadLetterActionResult(string Queue, string Action, int Count);
}