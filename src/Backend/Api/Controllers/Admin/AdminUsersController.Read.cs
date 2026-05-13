using Api.Models;
using Application.Modules.Users.Administration.Queries;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;

namespace Api.Controllers.Admin;

/// <summary>Admin queries (read model / read database).</summary>
public sealed partial class AdminUsersController
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AdminUserListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        Result<AdminUserListDto, Error> result = await Sender.Send(new AdminListUsersQuery(page, pageSize), cancellationToken);
        return MatchApi(result);
    }
}
