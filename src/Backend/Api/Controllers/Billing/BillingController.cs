using Api.Authorization;
using Api.Controllers.Base;
using Api.Filters;
using Api.Models;
using Api.Models.Billing;
using Application.Modules.Billing;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Billing;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Route("api/v{version:apiVersion}/billing")]
public sealed class BillingController : ApiControllerBase
{
    public BillingController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    ///     MVP checkout: provisions a new tenant and subscription for the authenticated user (owner).
    ///     Replace with payment-provider webhook flow in production.
    /// </summary>
    [HttpPost("subscribe")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<SubscribeToPlanResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeToPlanRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new SubscribeToPlanCommand(
                CurrentUserId,
                request.PlanCode,
                request.TenantSlug,
                request.TenantDisplayName,
                request.PublicTagline,
                request.PublicDescription),
            cancellationToken);
        return MatchApi(
            result,
            dto => OkApi(new SubscribeToPlanResponse(
                dto.TenantId,
                dto.TenantSlug,
                dto.SubscriptionId,
                dto.SubscriptionStatus)));
    }

    [HttpGet("tenants/{tenantId:guid}/subscription")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenantSubscription(Guid tenantId, CancellationToken cancellationToken)
    {
        var result =
            await Sender.Send(new GetTenantSubscriptionQuery(tenantId), cancellationToken);
        return MatchApi(result, dto => OkApi(BillingApiMapping.ToResponse(dto)));
    }

    [HttpGet("tenants/{tenantId:guid}/entitlements")]
    [ProducesResponseType(typeof(ApiResponse<TenantEntitlementsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenantEntitlements(Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new GetTenantEntitlementsQuery(tenantId, CurrentUserId),
            cancellationToken);
        return MatchApi(result, dto => OkApi(BillingApiMapping.ToEntitlementsResponse(dto)));
    }

    [HttpPost("tenants/{tenantId:guid}/subscription/cancel")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelSubscription(Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CancelTenantSubscriptionCommand(tenantId, CurrentUserId),
            cancellationToken);
        return MatchApi(result, dto => OkApi(BillingApiMapping.ToResponse(dto)));
    }

    [HttpPut("tenants/{tenantId:guid}/subscription/plan")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePlan(Guid tenantId, [FromBody] ChangeSubscriptionPlanRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new ChangeTenantSubscriptionPlanCommand(tenantId, CurrentUserId, request.PlanCode),
            cancellationToken);
        return MatchApi(result, dto => OkApi(BillingApiMapping.ToResponse(dto)));
    }

    [HttpGet("tenants/{tenantId:guid}/invoices")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BillingInvoiceResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTenantInvoices(
        Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new ListTenantBillingInvoicesQuery(tenantId, CurrentUserId, page, pageSize),
            cancellationToken);
        return MatchApi(result, rows => OkApi(rows.Select(BillingApiMapping.ToResponse).ToList()));
    }
}