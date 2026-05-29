using Application.Contracts.Authorization;
using Application.Contracts.Persistence;
using Application.Contracts.Support;
using Domains.Support;
using Microsoft.EntityFrameworkCore;

namespace Application.Modules.Support;

public sealed record AdminListSupportTicketsQuery(
    int Page,
    int PageSize,
    string? Search = null,
    SupportTicketStatus? Status = null,
    SupportTicketPriority? Priority = null,
    Guid? AssignedAdminUserId = null)
    : IRequest<Result<SupportTicketListDto, Error>>, IRequireAdmin;

public sealed class AdminListSupportTicketsQueryHandler
    : IRequestHandler<AdminListSupportTicketsQuery, Result<SupportTicketListDto, Error>>
{
    private readonly IApplicationReadDbContext _read;

    public AdminListSupportTicketsQueryHandler(IApplicationReadDbContext read)
    {
        _read = read;
    }

    public async Task<Result<SupportTicketListDto, Error>> Handle(AdminListSupportTicketsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 200)
            return new Error("BadRequest", 400, "Page must be at least 1 and page size between 1 and 200.");

        IQueryable<SupportTicket> q = _read.SupportTickets;
        if (request.Status is not null)
            q = q.Where(t => t.Status == request.Status.Value);
        if (request.Priority is not null)
            q = q.Where(t => t.Priority == request.Priority.Value);
        if (request.AssignedAdminUserId is not null)
            q = q.Where(t => t.AssignedAdminUserId == request.AssignedAdminUserId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToUpperInvariant();
            q = q.Where(t => t.NormalizedSubject.Contains(s) || t.TicketNumber.Contains(request.Search.Trim()));
        }

        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .OrderByDescending(t => t.UpdatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new SupportTicketSummaryDto(
                t.Id,
                t.TicketNumber,
                t.Subject,
                t.Category,
                t.Priority.ToString(),
                t.Status.ToString(),
                t.AssignedAdminUserId,
                t.CreatedAtUtc,
                t.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new SupportTicketListDto(items, total, request.Page, request.PageSize);
    }
}

public sealed record AdminGetSupportTicketByIdQuery(Guid TicketId)
    : IRequest<Result<SupportTicketDetailDto, Error>>, IRequireAdmin;

public sealed class AdminGetSupportTicketByIdQueryHandler
    : IRequestHandler<AdminGetSupportTicketByIdQuery, Result<SupportTicketDetailDto, Error>>
{
    private readonly IApplicationReadDbContext _read;

    public AdminGetSupportTicketByIdQueryHandler(IApplicationReadDbContext read)
    {
        _read = read;
    }

    public async Task<Result<SupportTicketDetailDto, Error>> Handle(AdminGetSupportTicketByIdQuery request,
        CancellationToken cancellationToken)
    {
        var ticket = await _read.SupportTickets
            .Where(t => t.Id == request.TicketId)
            .Select(t => new SupportTicketDetailDto(
                t.Id,
                t.TicketNumber,
                t.CreatedByUserId,
                t.AssignedAdminUserId,
                t.Subject,
                t.Description,
                t.Category,
                t.Priority.ToString(),
                t.Status.ToString(),
                t.CreatedAtUtc,
                t.UpdatedAtUtc,
                t.FirstResponseAtUtc,
                t.ClosedAtUtc,
                t.Messages
                    .OrderBy(m => m.CreatedAtUtc)
                    .Select(m => new SupportTicketMessageDto(
                        m.Id,
                        m.AuthorUserId,
                        m.Body,
                        m.IsInternalNote,
                        m.CreatedAtUtc,
                        m.Attachments
                            .Select(a =>
                                new SupportTicketAttachmentDto(a.Id, a.FileName, a.ContentType, a.ByteSize,
                                    a.StorageKey))
                            .ToList()))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
        return ticket is null ? new NotFoundError("Ticket not found.") : ticket;
    }
}

public sealed record AdminAssignSupportTicketCommand(Guid TicketId, Guid? AssignedAdminUserId)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class
    AdminAssignSupportTicketCommandHandler : IRequestHandler<AdminAssignSupportTicketCommand, Result<bool, Error>>
{
    private readonly ISupportTicketService _service;

    public AdminAssignSupportTicketCommandHandler(ISupportTicketService service)
    {
        _service = service;
    }

    public async Task<Result<bool, Error>> Handle(AdminAssignSupportTicketCommand request,
        CancellationToken cancellationToken)
    {
        var ok = await _service.AssignAsync(request.TicketId, request.AssignedAdminUserId, cancellationToken);
        return ok ? true : new NotFoundError("Ticket not found.");
    }
}

public sealed record AdminChangeSupportTicketStatusCommand(Guid TicketId, SupportTicketStatus Status)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class
    AdminChangeSupportTicketStatusCommandHandler : IRequestHandler<AdminChangeSupportTicketStatusCommand,
    Result<bool, Error>>
{
    private readonly ISupportTicketService _service;

    public AdminChangeSupportTicketStatusCommandHandler(ISupportTicketService service)
    {
        _service = service;
    }

    public async Task<Result<bool, Error>> Handle(AdminChangeSupportTicketStatusCommand request,
        CancellationToken cancellationToken)
    {
        var ok = await _service.ChangeStatusAsync(request.TicketId, request.Status, cancellationToken);
        return ok ? true : new NotFoundError("Ticket not found.");
    }
}

public sealed record AdminChangeSupportTicketPriorityCommand(Guid TicketId, SupportTicketPriority Priority)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class
    AdminChangeSupportTicketPriorityCommandHandler : IRequestHandler<AdminChangeSupportTicketPriorityCommand,
    Result<bool, Error>>
{
    private readonly ISupportTicketService _service;

    public AdminChangeSupportTicketPriorityCommandHandler(ISupportTicketService service)
    {
        _service = service;
    }

    public async Task<Result<bool, Error>> Handle(AdminChangeSupportTicketPriorityCommand request,
        CancellationToken cancellationToken)
    {
        var ok = await _service.ChangePriorityAsync(request.TicketId, request.Priority, cancellationToken);
        return ok ? true : new NotFoundError("Ticket not found.");
    }
}

public sealed record AdminAddSupportTicketMessageCommand(
    Guid TicketId,
    Guid AdminUserId,
    string Body,
    bool IsInternalNote,
    IReadOnlyList<SupportTicketAttachmentInput>? Attachments = null)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class
    AdminAddSupportTicketMessageCommandHandler : IRequestHandler<AdminAddSupportTicketMessageCommand,
    Result<bool, Error>>
{
    private readonly ISupportTicketService _service;

    public AdminAddSupportTicketMessageCommandHandler(ISupportTicketService service)
    {
        _service = service;
    }

    public async Task<Result<bool, Error>> Handle(AdminAddSupportTicketMessageCommand request,
        CancellationToken cancellationToken)
    {
        var attachments = request.Attachments?
            .Select(a => new SupportTicketAttachmentDraft(a.StorageKey, a.FileName, a.ContentType, a.ByteSize));
        var ok = await _service.AddMessageAsync(
            request.TicketId,
            request.AdminUserId,
            request.Body.Trim(),
            request.IsInternalNote,
            attachments,
            cancellationToken);
        return ok ? true : new NotFoundError("Ticket not found.");
    }
}