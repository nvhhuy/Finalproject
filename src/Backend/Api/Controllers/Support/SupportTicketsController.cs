using Api.Authorization;
using Api.Controllers.Base;
using Api.Filters;
using Api.Models;
using Application.Modules.Support;
using Asp.Versioning;
using Domains.Support;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;

namespace Api.Controllers.Support;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Route("api/v{version:apiVersion}/support/tickets")]
public sealed class SupportTicketsController : ApiControllerBase
{
    public SupportTicketsController(ISender sender) : base(sender) { }

    public sealed record CreateTicketRequest(
        string Subject,
        string Description,
        string? Category,
        string? Priority = null);

    public sealed record SupportTicketAttachmentRequest(
        string StorageKey,
        string FileName,
        string ContentType,
        long ByteSize);

    public sealed record AddMessageRequest(string Body, IReadOnlyList<SupportTicketAttachmentRequest>? Attachments = null);

    [HttpPost]
    [Transactional]
    [RequiresIdempotencyKey(Scope = "support.ticket.create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest request, CancellationToken cancellationToken)
    {
        SupportTicketPriority priority = SupportTicketPriority.Normal;
        if (!string.IsNullOrWhiteSpace(request.Priority)
            && !Enum.TryParse(request.Priority, ignoreCase: true, out priority))
        {
            return FailApi(new Error("BadRequest", 400, "Invalid priority value."));
        }

        Result<Guid, Error> result = await Sender.Send(
            new CreateSupportTicketCommand(CurrentUserId, request.Subject, request.Description, request.Category, priority),
            cancellationToken);
        return MatchApi(result, OkApi);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<SupportTicketListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] SupportTicketStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        Result<SupportTicketListDto, Error> result = await Sender.Send(
            new ListMySupportTicketsQuery(CurrentUserId, page, pageSize, search, status),
            cancellationToken);
        return MatchApi(result, OkApi);
    }

    [HttpGet("{ticketId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SupportTicketDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Detail(Guid ticketId, CancellationToken cancellationToken = default)
    {
        Result<SupportTicketDetailDto, Error> result = await Sender.Send(
            new GetMySupportTicketDetailQuery(CurrentUserId, ticketId),
            cancellationToken);
        return MatchApi(result, OkApi);
    }

    [HttpPost("{ticketId:guid}/messages")]
    [Transactional]
    [RequiresIdempotencyKey(Scope = "support.ticket.message")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddMessage(Guid ticketId, [FromBody] AddMessageRequest request, CancellationToken cancellationToken = default)
    {
        Result<bool, Error> result = await Sender.Send(
            new AddSupportTicketMessageCommand(
                CurrentUserId,
                ticketId,
                request.Body,
                request.Attachments?.Select(a => new SupportTicketAttachmentInput(a.StorageKey, a.FileName, a.ContentType, a.ByteSize)).ToList()),
            cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("{ticketId:guid}/close")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Close(Guid ticketId, CancellationToken cancellationToken = default)
    {
        Result<bool, Error> result = await Sender.Send(new CloseSupportTicketCommand(CurrentUserId, ticketId), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("{ticketId:guid}/reopen")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reopen(Guid ticketId, CancellationToken cancellationToken = default)
    {
        Result<bool, Error> result = await Sender.Send(new ReopenSupportTicketCommand(CurrentUserId, ticketId), cancellationToken);
        return MatchApiEmpty(result);
    }
}
