using Api.Authorization;
using Api.Controllers.Base;
using Api.Filters;
using Api.Models;
using Api.Models.Billing;
using Api.Models.Wallet;
using Application.Modules.Billing;
using Application.Modules.UserWallet;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;

namespace Api.Controllers.Wallet;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Route("api/v{version:apiVersion}/wallet")]
public sealed class WalletController : ApiControllerBase
{
    public WalletController(ISender sender) : base(sender) { }

    [HttpGet("balance")]
    [ProducesResponseType(typeof(ApiResponse<WalletBalanceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBalance(CancellationToken cancellationToken)
    {
        Result<Application.Contracts.Wallet.WalletBalanceDto, Error> result =
            await Sender.Send(new GetWalletBalanceQuery(CurrentUserId), cancellationToken);
        return MatchApi(result, dto => OkApi(WalletApiMapping.ToResponse(dto)));
    }

    [HttpGet("ledger")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<WalletLedgerEntryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLedger([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<Application.Contracts.Wallet.WalletLedgerEntryDto>, Error> result =
            await Sender.Send(new GetWalletLedgerQuery(CurrentUserId, page, pageSize), cancellationToken);
        return MatchApi(result, rows => OkApi(rows.Select(WalletApiMapping.ToResponse).ToList()));
    }

    [HttpGet("actions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MeteredActionResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListActions(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<Application.Contracts.Wallet.MeteredActionDto>, Error> result =
            await Sender.Send(new ListMeteredActionsQuery(), cancellationToken);
        return MatchApi(result, rows => OkApi(rows.Select(WalletApiMapping.ToResponse).ToList()));
    }

    /// <summary>MVP manual top-up (no payment provider). Production should use Stripe/PayPal webhooks.</summary>
    [HttpPost("top-up")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<WalletBalanceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> TopUp([FromBody] TopUpWalletRequest request, CancellationToken cancellationToken)
    {
        string idempotencyKey = ResolveIdempotencyKey();

        Result<Application.Contracts.Wallet.WalletBalanceDto, Error> result = await Sender.Send(
            new TopUpWalletCommand(CurrentUserId, request.AmountCredits, idempotencyKey),
            cancellationToken);
        return MatchApi(result, dto => OkApi(WalletApiMapping.ToResponse(dto)));
    }

    /// <summary>Purchase a catalog credit package (MVP: immediate fulfillment; production should require payment webhook).</summary>
    [HttpPost("purchase")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<WalletBalanceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Purchase([FromBody] PurchaseCreditPackageRequest request, CancellationToken cancellationToken)
    {
        Result<Application.Contracts.Wallet.WalletBalanceDto, Error> result = await Sender.Send(
            new Application.Modules.Billing.PurchaseCreditPackageCommand(
                CurrentUserId,
                request.PackageCode,
                ResolveIdempotencyKey()),
            cancellationToken);
        return MatchApi(result, dto => OkApi(WalletApiMapping.ToResponse(dto)));
    }

    [HttpPost("debit")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<WalletDebitResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Debit([FromBody] DebitWalletActionRequest request, CancellationToken cancellationToken)
    {
        Result<Application.Contracts.Wallet.WalletDebitResultDto, Error> result = await Sender.Send(
            new DebitWalletActionCommand(CurrentUserId, request.ActionCode, ResolveIdempotencyKey()),
            cancellationToken);
        return MatchApi(result, dto => OkApi(WalletApiMapping.ToResponse(dto)));
    }

    private string ResolveIdempotencyKey()
    {
        if (Request.Headers.TryGetValue("Idempotency-Key", out var standard) && !string.IsNullOrWhiteSpace(standard))
            return standard.ToString();

        if (Request.Headers.TryGetValue("X-Idempotency-Key", out var legacy) && !string.IsNullOrWhiteSpace(legacy))
            return legacy.ToString();

        return Guid.NewGuid().ToString("N");
    }

    [HttpGet("invoices")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BillingInvoiceResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListInvoices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<BillingInvoiceListItemDto>, Error> result = await Sender.Send(
            new ListMyWalletInvoicesQuery(CurrentUserId, page, pageSize),
            cancellationToken);
        return MatchApi(result, rows => OkApi(rows.Select(BillingApiMapping.ToResponse).ToList()));
    }
}
