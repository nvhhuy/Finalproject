using Api.Models;
using Application.Modules.Users.Profile.Queries;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;

namespace Api.Controllers.Users;

/// <summary>Current-user queries (read stack).</summary>
public sealed partial class UsersController
{
    [HttpGet("profile")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        Result<UserProfileDto, Error> result = await Sender.Send(new GetProfileQuery(CurrentUserId), cancellationToken);
        return MatchApi(result);
    }
}
