using System.Text;
using System.Text.Json;
using Api.Controllers.Base;
using Api.Models;
using Api.Models.Billing;
using Application.Modules.Billing;
using Asp.Versioning;
using Infrastructure.Modules.Billing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Shared.Errors.General;

namespace Api.Controllers.Billing;

[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/webhooks/stripe")]
public sealed class StripeWebhooksController : ApiControllerBase
{
    private readonly StripeWebhookOptions _options;

    public StripeWebhooksController(ISender sender, IOptions<StripeWebhookOptions> options) : base(sender)
    {
        _options = options.Value;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PaymentWebhookReceiptResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
            return BadRequestValidationApi(new ValidationError("body", "Empty webhook payload."));

        if (_options.RequireSignature)
        {
            if (!Request.Headers.TryGetValue("Stripe-Signature", out var signatureValues))
                return BadRequestValidationApi(new ValidationError("Stripe-Signature",
                    "Header is required when webhook secret is configured."));

            var signature = signatureValues.ToString();
            if (!StripeWebhookSignatureVerifier.TryVerify(
                    payload,
                    signature,
                    _options.WebhookSecret!,
                    TimeSpan.FromMinutes(5)))
                return FailApi(new ForbiddenError("Invalid Stripe webhook signature."));
        }

        string? eventId = null;
        string? eventType = null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                eventId = idEl.GetString();
            if (doc.RootElement.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                eventType = typeEl.GetString();
        }
        catch (JsonException)
        {
            return BadRequestValidationApi(new ValidationError("body", "Invalid JSON payload."));
        }

        var result = await Sender.Send(
            new ReceivePaymentWebhookCommand("stripe", payload, eventId, eventType),
            cancellationToken);

        return MatchApi(
            result,
            dto => OkApi(new PaymentWebhookReceiptResponse(dto.EventId, dto.Duplicate, dto.Status)));
    }
}