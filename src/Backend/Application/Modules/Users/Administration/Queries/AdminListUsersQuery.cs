using Application.Common;
using Application.Contracts.Modules.Users.Persistence.Read;
using Domains.UserAggregate;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Users.Administration.Queries;

public sealed record AdminListUsersQuery(
    int Page,
    int PageSize,
    string? EmailContains = null,
    string? UserNameContains = null,
    bool? IsLocked = null,
    string? Role = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<Result<AdminUserListDto, Error>>;

public sealed record AdminUserSummaryDto(
    Guid Id,
    string UserName,
    string Email,
    string Role,
    bool IsLocked,
    DateTime? EmailVerifiedAtUtc,
    DateTime? RegisteredAtUtc);

public sealed record AdminUserListDto(IReadOnlyList<AdminUserSummaryDto> Items, int TotalCount, int Page, int PageSize);

public sealed class AdminListUsersQueryHandler : IRequestHandler<AdminListUsersQuery, Result<AdminUserListDto, Error>>
{
    private readonly IUserReadRepository _users;

    public AdminListUsersQueryHandler(IUserReadRepository users) => _users = users;

    public async Task<Result<AdminUserListDto, Error>> Handle(AdminListUsersQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.PageSize < 1 || request.PageSize > 200)
            return new Error("BadRequest", 400, "Page must be at least 1 and page size between 1 and 200.");

        var p = new AdminUserListQueryParams(
            request.Page,
            request.PageSize,
            request.EmailContains,
            request.UserNameContains,
            request.IsLocked,
            request.Role,
            request.SortBy,
            request.SortDescending);

        PagedList<User> page = await _users.GetUsersAdminPageAsync(p, cancellationToken);

        AdminUserSummaryDto[] items = page
            .Select(u => new AdminUserSummaryDto(
                u.Id.Value,
                u.UserName,
                u.Email,
                u.Role,
                u.IsLocked,
                u.EmailVerifiedAtUtc,
                u.RegisteredAtUtc))
            .ToArray();
        return new AdminUserListDto(items, page.TotalCount, request.Page, request.PageSize);
    }
}
