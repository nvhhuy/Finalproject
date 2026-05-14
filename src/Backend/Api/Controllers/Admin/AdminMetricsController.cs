using Api.Authorization;
using Api.Controllers.Base;
using Api.Models;
using Application.Modules.Users.Administration.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;

namespace Api.Controllers.Admin;

[ApiController]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/admin/metrics")]
public sealed class AdminMetricsController : ApiControllerBase
{
    public AdminMetricsController(ISender sender) : base(sender)
    {
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardMetricsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        Result<AdminDashboardMetricsDto, Error> result = await Sender.Send(new AdminDashboardMetricsQuery(), cancellationToken);
        return MatchApi(result);
    }
}
