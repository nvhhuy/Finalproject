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
[Route("api/v{version:apiVersion}/platform/credit-packages")]
public sealed class PlatformCreditPackagesController : ApiControllerBase
{
    public PlatformCreditPackagesController(ISender sender) : base(sender) { }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CreditPackageResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<CreditPackageDto>, Error> result =
            await Sender.Send(new ListCreditPackagesQuery(ActiveOnly: false), cancellationToken);
        return MatchApi(result, rows => OkApi(rows.Select(BillingApiMapping.ToResponse).ToList()));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreditPackageResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateCreditPackageRequest request, CancellationToken cancellationToken)
    {
        Result<CreditPackageDto, Error> result = await Sender.Send(
            new CreateCreditPackageCommand(
                request.Code,
                request.Name,
                request.Description,
                request.PriceCents,
                request.Currency,
                request.CreditsAmount,
                request.SortOrder),
            cancellationToken);
        return MatchApi(result, dto => OkApi(BillingApiMapping.ToResponse(dto)));
    }

    [HttpPut("{packageId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CreditPackageResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid packageId, [FromBody] UpdateCreditPackageRequest request, CancellationToken cancellationToken)
    {
        Result<CreditPackageDto, Error> result = await Sender.Send(
            new UpdateCreditPackageCommand(
                packageId,
                request.Name,
                request.Description,
                request.PriceCents,
                request.Currency,
                request.CreditsAmount,
                request.IsActive,
                request.SortOrder),
            cancellationToken);
        return MatchApi(result, dto => OkApi(BillingApiMapping.ToResponse(dto)));
    }

    [HttpDelete("{packageId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid packageId, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new DeleteCreditPackageCommand(packageId), cancellationToken);
        return MatchApi(result, _ => OkApi(true));
    }
}
