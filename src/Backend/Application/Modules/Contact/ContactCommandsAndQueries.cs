using Application.Contracts.Authorization;
using Application.Contracts.Contact;
using Application.Contracts.Persistence;
using Domains.Contact;
using Microsoft.EntityFrameworkCore;

namespace Application.Modules.Contact;

// ---------------------------------------------------------------------------
// Public command: submit a contact message (no auth required)
// ---------------------------------------------------------------------------

public sealed record SubmitContactMessageCommand(
    string Name,
    string Email,
    string Subject,
    string Body)
    : IRequest<Result<Guid, Error>>;

public sealed class SubmitContactMessageCommandHandler
    : IRequestHandler<SubmitContactMessageCommand, Result<Guid, Error>>
{
    private readonly IContactMessageService _service;

    public SubmitContactMessageCommandHandler(IContactMessageService service)
    {
        _service = service;
    }

    public async Task<Result<Guid, Error>> Handle(
        SubmitContactMessageCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Subject) ||
            string.IsNullOrWhiteSpace(request.Body))
            return new Error("BadRequest", 400, "All fields are required.");

        var id = await _service.SubmitAsync(
            request.Name,
            request.Email,
            request.Subject,
            request.Body,
            cancellationToken);
        return id;
    }
}

// ---------------------------------------------------------------------------
// Admin query: list contact messages
// ---------------------------------------------------------------------------

public sealed record AdminListContactMessagesQuery(
    int Page,
    int PageSize,
    string? Search = null,
    bool? IsRead = null)
    : IRequest<Result<ContactMessageListDto, Error>>, IRequireAdmin;

public sealed class AdminListContactMessagesQueryHandler
    : IRequestHandler<AdminListContactMessagesQuery, Result<ContactMessageListDto, Error>>
{
    private readonly IApplicationReadDbContext _read;

    public AdminListContactMessagesQueryHandler(IApplicationReadDbContext read)
    {
        _read = read;
    }

    public async Task<Result<ContactMessageListDto, Error>> Handle(
        AdminListContactMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 200)
            return new Error("BadRequest", 400, "Page must be at least 1 and page size between 1 and 200.");

        IQueryable<ContactMessage> q = _read.ContactMessages;

        if (request.IsRead.HasValue)
            q = q.Where(m => m.IsRead == request.IsRead.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var upper = request.Search.Trim().ToUpperInvariant();
            q = q.Where(m =>
                m.NormalizedEmail.Contains(upper) ||
                m.Name.ToUpper().Contains(upper) ||
                m.Subject.ToUpper().Contains(upper));
        }

        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .OrderByDescending(m => m.SubmittedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new ContactMessageSummaryDto(
                m.Id,
                m.Name,
                m.Email,
                m.Subject,
                m.Body,
                m.IsRead,
                m.SubmittedAtUtc))
            .ToListAsync(cancellationToken);

        return new ContactMessageListDto(items, total, request.Page, request.PageSize);
    }
}

// ---------------------------------------------------------------------------
// Admin command: mark a contact message as read
// ---------------------------------------------------------------------------

public sealed record AdminMarkContactMessageReadCommand(Guid MessageId)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class AdminMarkContactMessageReadCommandHandler
    : IRequestHandler<AdminMarkContactMessageReadCommand, Result<bool, Error>>
{
    private readonly IContactMessageService _service;

    public AdminMarkContactMessageReadCommandHandler(IContactMessageService service)
    {
        _service = service;
    }

    public async Task<Result<bool, Error>> Handle(
        AdminMarkContactMessageReadCommand request,
        CancellationToken cancellationToken)
    {
        var ok = await _service.MarkReadAsync(request.MessageId, cancellationToken);
        return ok ? true : new NotFoundError("Contact message not found.");
    }
}
