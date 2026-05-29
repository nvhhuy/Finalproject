using Api.Authorization;
using Api.Controllers.Base;
using Api.Filters;
using Api.Models;
using Application.Modules.Support;
using Asp.Versioning;
using Domains.Support;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Errors;

namespace Api.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v{version:apiVersion}/admin/support/tickets")]
public sealed class AdminSupportTicketsController : ApiControllerBase
{
    public AdminSupportTicketsController(ISender sender) : base(sender)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<SupportTicketListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] SupportTicketStatus? status = null,
        [FromQuery] SupportTicketPriority? priority = null,
        [FromQuery] Guid? assignedAdminUserId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new AdminListSupportTicketsQuery(page, pageSize, search, status, priority, assignedAdminUserId),
            cancellationToken);
        return MatchApi(result, OkApi);
    }

    [HttpGet("{ticketId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SupportTicketDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Detail(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new AdminGetSupportTicketByIdQuery(ticketId), cancellationToken);
        return MatchApi(result, OkApi);
    }

    [HttpPost("{ticketId:guid}/assign")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Assign(Guid ticketId, [FromBody] AssignRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new AdminAssignSupportTicketCommand(ticketId, request.AssignedAdminUserId),
            cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("{ticketId:guid}/status")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeStatus(Guid ticketId, [FromBody] ChangeStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse(request.Status, true, out SupportTicketStatus status))
            return FailApi(new Error("BadRequest", 400, "Invalid status value."));

        var result = await Sender.Send(
            new AdminChangeSupportTicketStatusCommand(ticketId, status),
            cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("{ticketId:guid}/priority")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePriority(Guid ticketId, [FromBody] ChangePriorityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse(request.Priority, true, out SupportTicketPriority priority))
            return FailApi(new Error("BadRequest", 400, "Invalid priority value."));

        var result = await Sender.Send(
            new AdminChangeSupportTicketPriorityCommand(ticketId, priority),
            cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("{ticketId:guid}/messages")]
    [Transactional]
    [RequiresIdempotencyKey(Scope = "support.ticket.admin.message")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddMessage(Guid ticketId, [FromBody] AddMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new AdminAddSupportTicketMessageCommand(
                ticketId,
                CurrentAdminId,
                request.Body,
                request.IsInternalNote,
                request.Attachments?.Select(a =>
                    new SupportTicketAttachmentInput(a.StorageKey, a.FileName, a.ContentType, a.ByteSize)).ToList()),
            cancellationToken);
        return MatchApiEmpty(result);
    }

    public sealed record AssignRequest(Guid? AssignedAdminUserId);

    public sealed record ChangeStatusRequest(string Status);

    public sealed record ChangePriorityRequest(string Priority);

    public sealed record SupportTicketAttachmentRequest(
        string StorageKey,
        string FileName,
        string ContentType,
        long ByteSize);

    public sealed record AddMessageRequest(
        string Body,
        bool IsInternalNote = false,
        IReadOnlyList<SupportTicketAttachmentRequest>? Attachments = null);
}