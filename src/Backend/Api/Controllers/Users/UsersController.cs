using Api.Authorization;
using Api.Controllers.Base;
using Api.Filters;
using Api.Models;
using Api.Models.Users;
using Application.Modules.Users.Profile.Commands;
using Application.Modules.Users.Profile.Queries;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Users;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Authorize]
[Route("api/v{version:apiVersion}/users")]
public sealed class UsersController : ApiControllerBase
{
    public UsersController(ISender sender) : base(sender)
    {
    }

    [HttpGet("profile")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetProfileQuery(CurrentUserId), cancellationToken);
        return MatchApi(result);
    }

    [HttpPut("profile")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest body,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new UpdateMyProfileCommand(CurrentUserId, body.FirstName, body.LastName, body.RowVersion),
            cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPut("profile/avatar")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetAvatar([FromBody] SetAvatarRequest body, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new SetMyAvatarCommand(CurrentUserId, body.MediaAssetId, body.RowVersion),
            cancellationToken);
        return MatchApiEmpty(result);
    }
}