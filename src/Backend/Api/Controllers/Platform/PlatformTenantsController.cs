using Api.Authorization;
using Api.Controllers.Base;
using Api.Models;
using Api.Models.Tenancy;
using Application.Modules.Tenancy;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;

namespace Api.Controllers.Platform;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v{version:apiVersion}/platform/tenants")]
public sealed class PlatformTenantsController : ApiControllerBase
{
    public PlatformTenantsController(ISender sender) : base(sender) { }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TenantSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<TenantSummaryDto>, Error> result =
            await Sender.Send(new ListTenantsQuery(page, pageSize), cancellationToken);
        return MatchApi(result, rows => OkApi(rows.Select(TenantApiMapping.ToResponse).ToList()));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TenantSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        Result<TenantSummaryDto, Error> result = await Sender.Send(
            new CreateTenantCommand(
                request.Slug,
                request.DisplayName,
                request.OwnerUserId,
                request.PublicTagline,
                request.PublicDescription),
            cancellationToken);
        return MatchApi(result, dto => OkApi(TenantApiMapping.ToResponse(dto)));
    }
}
