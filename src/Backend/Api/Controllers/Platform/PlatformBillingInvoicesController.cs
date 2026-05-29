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
[Route("api/v{version:apiVersion}/platform/invoices")]
public sealed class PlatformBillingInvoicesController : ApiControllerBase
{
    public PlatformBillingInvoicesController(ISender sender) : base(sender) { }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BillingInvoiceResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<BillingInvoiceListItemDto>, Error> result = await Sender.Send(
            new ListPlatformBillingInvoicesQuery(page, pageSize),
            cancellationToken);
        return MatchApi(result, rows => OkApi(rows.Select(BillingApiMapping.ToResponse).ToList()));
    }
}
