namespace Application.Modules.Support;

public sealed record SupportTicketAttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long ByteSize,
    string StorageKey);

public sealed record SupportTicketMessageDto(
    Guid Id,
    Guid AuthorUserId,
    string Body,
    bool IsInternalNote,
    DateTime CreatedAtUtc,
    IReadOnlyList<SupportTicketAttachmentDto> Attachments);

public sealed record SupportTicketSummaryDto(
    Guid Id,
    string TicketNumber,
    string Subject,
    string? Category,
    string Priority,
    string Status,
    Guid? AssignedAdminUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record SupportTicketDetailDto(
    Guid Id,
    string TicketNumber,
    Guid CreatedByUserId,
    Guid? AssignedAdminUserId,
    string Subject,
    string Description,
    string? Category,
    string Priority,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? FirstResponseAtUtc,
    DateTime? ClosedAtUtc,
    IReadOnlyList<SupportTicketMessageDto> Messages);

public sealed record SupportTicketListDto(
    IReadOnlyList<SupportTicketSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);