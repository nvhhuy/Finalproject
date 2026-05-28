using Application.Contracts.Persistence;
using Application.Contracts.Support;
using Domains.Support;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Support;

public sealed record CreateSupportTicketCommand(
    Guid UserId,
    string Subject,
    string Description,
    string? Category,
    SupportTicketPriority Priority) : IRequest<Result<Guid, Error>>;

public sealed class CreateSupportTicketCommandHandler : IRequestHandler<CreateSupportTicketCommand, Result<Guid, Error>>
{
    private readonly ISupportTicketService _service;
    public CreateSupportTicketCommandHandler(ISupportTicketService service) => _service = service;

    public async Task<Result<Guid, Error>> Handle(CreateSupportTicketCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Description))
            return new Error("BadRequest", 400, "Subject and description are required.");

        Guid id = await _service.CreateAsync(
            request.UserId,
            request.Subject.Trim(),
            request.Description.Trim(),
            request.Category?.Trim(),
            request.Priority,
            cancellationToken);
        return id;
    }
}

public sealed record AddSupportTicketMessageCommand(
    Guid UserId,
    Guid TicketId,
    string Body,
    IReadOnlyList<SupportTicketAttachmentInput>? Attachments = null) : IRequest<Result<bool, Error>>;

public sealed record SupportTicketAttachmentInput(
    string StorageKey,
    string FileName,
    string ContentType,
    long ByteSize);

public sealed class AddSupportTicketMessageCommandHandler : IRequestHandler<AddSupportTicketMessageCommand, Result<bool, Error>>
{
    private readonly IApplicationReadDbContext _read;
    private readonly ISupportTicketService _service;

    public AddSupportTicketMessageCommandHandler(IApplicationReadDbContext read, ISupportTicketService service)
    {
        _read = read;
        _service = service;
    }

    public async Task<Result<bool, Error>> Handle(AddSupportTicketMessageCommand request, CancellationToken cancellationToken)
    {
        bool owns = await _read.SupportTickets
            .AnyAsync(t => t.Id == request.TicketId && t.CreatedByUserId == request.UserId, cancellationToken);
        if (!owns)
            return new NotFoundError("Ticket not found.");

        IEnumerable<SupportTicketAttachmentDraft>? attachments = request.Attachments?
            .Select(a => new SupportTicketAttachmentDraft(a.StorageKey, a.FileName, a.ContentType, a.ByteSize));

        bool ok = await _service.AddMessageAsync(
            request.TicketId,
            request.UserId,
            request.Body.Trim(),
            isInternalNote: false,
            attachments,
            cancellationToken);
        return ok ? true : new NotFoundError("Ticket not found.");
    }
}

public sealed record CloseSupportTicketCommand(Guid UserId, Guid TicketId) : IRequest<Result<bool, Error>>;
public sealed record ReopenSupportTicketCommand(Guid UserId, Guid TicketId) : IRequest<Result<bool, Error>>;

public sealed class CloseSupportTicketCommandHandler : IRequestHandler<CloseSupportTicketCommand, Result<bool, Error>>
{
    private readonly IApplicationReadDbContext _read;
    private readonly ISupportTicketService _service;
    public CloseSupportTicketCommandHandler(IApplicationReadDbContext read, ISupportTicketService service)
    {
        _read = read;
        _service = service;
    }

    public async Task<Result<bool, Error>> Handle(CloseSupportTicketCommand request, CancellationToken cancellationToken)
    {
        bool owns = await _read.SupportTickets.AnyAsync(t => t.Id == request.TicketId && t.CreatedByUserId == request.UserId, cancellationToken);
        if (!owns)
            return new NotFoundError("Ticket not found.");
        bool ok = await _service.ChangeStatusAsync(request.TicketId, SupportTicketStatus.Closed, cancellationToken);
        return ok ? true : new NotFoundError("Ticket not found.");
    }
}

public sealed class ReopenSupportTicketCommandHandler : IRequestHandler<ReopenSupportTicketCommand, Result<bool, Error>>
{
    private readonly IApplicationReadDbContext _read;
    private readonly ISupportTicketService _service;
    public ReopenSupportTicketCommandHandler(IApplicationReadDbContext read, ISupportTicketService service)
    {
        _read = read;
        _service = service;
    }

    public async Task<Result<bool, Error>> Handle(ReopenSupportTicketCommand request, CancellationToken cancellationToken)
    {
        bool owns = await _read.SupportTickets.AnyAsync(t => t.Id == request.TicketId && t.CreatedByUserId == request.UserId, cancellationToken);
        if (!owns)
            return new NotFoundError("Ticket not found.");
        bool ok = await _service.ChangeStatusAsync(request.TicketId, SupportTicketStatus.Open, cancellationToken);
        return ok ? true : new NotFoundError("Ticket not found.");
    }
}

public sealed record ListMySupportTicketsQuery(
    Guid UserId,
    int Page,
    int PageSize,
    string? Search,
    SupportTicketStatus? Status) : IRequest<Result<SupportTicketListDto, Error>>;

public sealed class ListMySupportTicketsQueryHandler
    : IRequestHandler<ListMySupportTicketsQuery, Result<SupportTicketListDto, Error>>
{
    private readonly IApplicationReadDbContext _read;
    public ListMySupportTicketsQueryHandler(IApplicationReadDbContext read) => _read = read;

    public async Task<Result<SupportTicketListDto, Error>> Handle(ListMySupportTicketsQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 200)
            return new Error("BadRequest", 400, "Page must be at least 1 and page size between 1 and 200.");

        IQueryable<SupportTicket> q = _read.SupportTickets.Where(t => t.CreatedByUserId == request.UserId);
        if (request.Status is not null)
            q = q.Where(t => t.Status == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string s = request.Search.Trim().ToUpperInvariant();
            q = q.Where(t => t.NormalizedSubject.Contains(s) || t.TicketNumber.Contains(request.Search.Trim()));
        }

        int total = await q.CountAsync(cancellationToken);
        List<SupportTicketSummaryDto> items = await q
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

public sealed record GetMySupportTicketDetailQuery(Guid UserId, Guid TicketId) : IRequest<Result<SupportTicketDetailDto, Error>>;

public sealed class GetMySupportTicketDetailQueryHandler
    : IRequestHandler<GetMySupportTicketDetailQuery, Result<SupportTicketDetailDto, Error>>
{
    private readonly IApplicationReadDbContext _read;
    public GetMySupportTicketDetailQueryHandler(IApplicationReadDbContext read) => _read = read;

    public async Task<Result<SupportTicketDetailDto, Error>> Handle(GetMySupportTicketDetailQuery request, CancellationToken cancellationToken)
    {
        SupportTicketDetailDto? ticket = await _read.SupportTickets
            .Where(t => t.Id == request.TicketId && t.CreatedByUserId == request.UserId)
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
                    .Where(m => !m.IsInternalNote)
                    .Select(m => new SupportTicketMessageDto(
                        m.Id,
                        m.AuthorUserId,
                        m.Body,
                        m.IsInternalNote,
                        m.CreatedAtUtc,
                        m.Attachments
                            .Select(a => new SupportTicketAttachmentDto(a.Id, a.FileName, a.ContentType, a.ByteSize, a.StorageKey))
                            .ToList()))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return ticket is null ? new NotFoundError("Ticket not found.") : ticket;
    }
}
