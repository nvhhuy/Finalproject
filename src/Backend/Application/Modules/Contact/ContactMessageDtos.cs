namespace Application.Modules.Contact;

public sealed record ContactMessageSummaryDto(
    Guid Id,
    string Name,
    string Email,
    string Subject,
    string Body,
    bool IsRead,
    DateTime SubmittedAtUtc);

public sealed record ContactMessageListDto(
    IReadOnlyList<ContactMessageSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
