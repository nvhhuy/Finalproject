using Application.Contracts.Authorization;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Mapping;

namespace Application.Modules.Users.Administration.Queries;

public sealed record AdminListUsersQuery(
    int Page,
    int PageSize,
    string? EmailContains = null,
    string? UserNameContains = null,
    string? Search = null,
    bool? IsLocked = null,
    string? Role = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<Result<AdminUserListDto, Error>>, IRequireAdmin;

public sealed record AdminUserSummaryDto(
    Guid Id,
    string UserName,
    string Email,
    string Role,
    bool IsLocked,
    DateTime? EmailVerifiedAtUtc,
    DateTime? RegisteredAtUtc,
    uint RowVersion);

public sealed record AdminUserListDto(IReadOnlyList<AdminUserSummaryDto> Items, int TotalCount, int Page, int PageSize);

public sealed class AdminListUsersQueryHandler : IRequestHandler<AdminListUsersQuery, Result<AdminUserListDto, Error>>
{
    private readonly IUserReadRepository _users;

    public AdminListUsersQueryHandler(IUserReadRepository users)
    {
        _users = users;
    }

    public async Task<Result<AdminUserListDto, Error>> Handle(AdminListUsersQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.PageSize < 1 || request.PageSize > 200)
            return new Error("BadRequest", 400, "Page must be at least 1 and page size between 1 and 200.");

        var p = new AdminUserListQueryParams(
            request.Page,
            request.PageSize,
            request.EmailContains,
            request.UserNameContains,
            request.Search,
            request.IsLocked,
            request.Role,
            request.SortBy,
            request.SortDescending);

        var page = await _users.GetUsersAdminPageAsync(p, cancellationToken);

        var items = page.ToAdminSummaryDtos();
        return new AdminUserListDto(items, page.TotalCount, request.Page, request.PageSize);
    }
}