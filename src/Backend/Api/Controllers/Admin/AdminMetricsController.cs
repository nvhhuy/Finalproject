using Api.Authorization;
using Api.Controllers.Base;
using Api.Models;
using Application.Modules.Users.Administration.Queries;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v{version:apiVersion}/admin/metrics")]
public sealed class AdminMetricsController : ApiControllerBase
{
    public AdminMetricsController(ISender sender) : base(sender)
    {
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardMetricsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new AdminDashboardMetricsQuery(), cancellationToken);
        return MatchApi(result);
    }
}