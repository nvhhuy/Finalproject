using Api.Authorization;
using Api.Controllers.Base;
using Api.Models;
using Api.Models.Billing;
using Application.Modules.Billing;
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
[Route("api/v{version:apiVersion}/platform/plans")]
public sealed class PlatformPlansController : ApiControllerBase
{
    public PlatformPlansController(ISender sender) : base(sender) { }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PlanResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<PlanDto>, Error> result =
            await Sender.Send(new ListPlansQuery(ActiveOnly: false), cancellationToken);
        return MatchApi(result, rows => OkApi(rows.Select(BillingApiMapping.ToResponse).ToList()));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PlanResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreatePlanRequest request, CancellationToken cancellationToken)
    {
        Result<PlanDto, Error> result = await Sender.Send(
            new CreatePlanCommand(
                request.Code,
                request.Name,
                request.Description,
                request.PriceMonthlyCents,
                request.Currency,
                request.TrialDays,
                request.Limits.ToPlanLimits(),
                request.SortOrder),
            cancellationToken);
        return MatchApi(result, dto => OkApi(BillingApiMapping.ToResponse(dto)));
    }

    [HttpPut("{planId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PlanResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid planId, [FromBody] UpdatePlanRequest request, CancellationToken cancellationToken)
    {
        Result<PlanDto, Error> result = await Sender.Send(
            new UpdatePlanCommand(
                planId,
                request.Name,
                request.Description,
                request.PriceMonthlyCents,
                request.Currency,
                request.TrialDays,
                request.Limits.ToPlanLimits(),
                request.IsActive,
                request.SortOrder),
            cancellationToken);
        return MatchApi(result, dto => OkApi(BillingApiMapping.ToResponse(dto)));
    }

    [HttpDelete("{planId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid planId, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new DeletePlanCommand(planId), cancellationToken);
        return MatchApi(result, _ => OkApi(true));
    }
}
