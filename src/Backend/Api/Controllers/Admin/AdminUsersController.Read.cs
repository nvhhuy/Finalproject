using Api.Models;
using Application.Modules.Users.Administration.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Admin;

/// <summary>Admin queries (read model / read database).</summary>
public sealed partial class AdminUsersController
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AdminUserListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? emailContains = null,
        [FromQuery] string? userNameContains = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? isLocked = null,
        [FromQuery] string? role = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new AdminListUsersQuery(page, pageSize, emailContains, userNameContains, search, isLocked, role, sortBy,
                sortDescending),
            cancellationToken);
        return MatchApi(result);
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(Guid userId, CancellationToken cancellationToken)
    {
        var result =
            await Sender.Send(new AdminGetUserByIdQuery(userId), cancellationToken);
        return MatchApi(result);
    }
}