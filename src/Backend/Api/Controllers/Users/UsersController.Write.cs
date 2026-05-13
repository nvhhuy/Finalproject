using Api.Filters;
using Api.Models;
using Api.Models.Users;
using Application.Modules.Users.Profile.Commands;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;

namespace Api.Controllers.Users;

/// <summary>Current-user commands (write stack).</summary>
public sealed partial class UsersController
{
    [HttpPut("profile")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest body, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(
            new UpdateMyProfileCommand(CurrentUserId, body.FirstName, body.LastName),
            cancellationToken);
        return MatchApiEmpty(result);
    }
}
