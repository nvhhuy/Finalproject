using Api.Authorization;
using Api.Controllers.Base;
using Api.Models;
using Application.Modules.Users.Administration.Queries;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;

namespace Api.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v{version:apiVersion}/admin/audit-logs")]
public sealed class AdminAuditLogsController : ApiControllerBase
{
    public AdminAuditLogsController(ISender sender) : base(sender)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AdminAuditLogListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? targetUserId = null,
        CancellationToken cancellationToken = default)
    {
        Result<AdminAuditLogListDto, Error> result =
            await Sender.Send(new AdminListAuditLogsQuery(page, pageSize, targetUserId), cancellationToken);
        return MatchApi(result);
    }
}
