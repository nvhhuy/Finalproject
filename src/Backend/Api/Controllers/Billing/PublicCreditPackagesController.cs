using Api.Controllers.Base;
using Api.Models;
using Api.Models.Billing;
using Application.Modules.Billing;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Billing;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public/credit-packages")]
public sealed class PublicCreditPackagesController : ApiControllerBase
{
    public PublicCreditPackagesController(ISender sender) : base(sender)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CreditPackageResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListActive(CancellationToken cancellationToken)
    {
        var result =
            await Sender.Send(new ListCreditPackagesQuery(), cancellationToken);
        return MatchApi(result, rows => OkApi(rows.Select(BillingApiMapping.ToResponse).ToList()));
    }
}