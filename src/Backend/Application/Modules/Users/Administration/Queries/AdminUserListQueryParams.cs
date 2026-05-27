namespace Application.Modules.Users.Administration.Queries;

public sealed record AdminUserListQueryParams(
    int Page,
    int PageSize,
    string? EmailContains,
    string? UserNameContains,
    string? Search,
    bool? IsLocked,
    string? Role,
    string? SortBy,
    bool SortDescending);
