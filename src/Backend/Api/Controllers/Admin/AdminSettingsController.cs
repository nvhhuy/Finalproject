using Api.Authorization;
using Api.Controllers.Base;
using Api.Models;
using Api.Models.Admin;
using Application.Modules.SystemSettings;
using Application.Modules.SystemSettings.Admin.Commands;
using Application.Modules.SystemSettings.Admin.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;

namespace Api.Controllers.Admin;

[ApiController]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/admin/settings")]
public sealed class AdminSettingsController : ApiControllerBase
{
    public AdminSettingsController(ISender sender) : base(sender)
    {
    }

    [HttpGet("allow-public-register")]
    [ProducesResponseType(typeof(ApiResponse<BooleanSettingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllowPublicRegister(CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(
            new GetBooleanAppSettingQuery(AppSettingKeys.AllowPublicRegister, DefaultValue: true),
            cancellationToken);
        return MatchApi(result, value => OkApi(new BooleanSettingDto(AppSettingKeys.AllowPublicRegister, value)));
    }

    [HttpPut("allow-public-register")]
    [ProducesResponseType(typeof(ApiResponse<BooleanSettingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetAllowPublicRegister([FromBody] SetBooleanSettingRequest body, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(
            new SetBooleanAppSettingCommand(AppSettingKeys.AllowPublicRegister, body.Value),
            cancellationToken);
        return MatchApi(result, _ => OkApi(new BooleanSettingDto(AppSettingKeys.AllowPublicRegister, body.Value)));
    }
}
