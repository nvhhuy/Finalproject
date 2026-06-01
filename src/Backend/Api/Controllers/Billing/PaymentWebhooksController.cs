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
[Route("api/v{version:apiVersion}/webhooks/vnpay")]
public sealed class VnPayWebhooksController : ApiControllerBase
{
    private readonly VnPayWebhookOptions _options;

    public VnPayWebhooksController(ISender sender, IOptions<VnPayWebhookOptions> options) : base(sender)
    {
        _options = options.Value;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PaymentWebhookReceiptResponse>), StatusCodes.Status200OK)]
    public Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        return ReceiveAsync("vnpay", "X-VNPay-Signature", _options.RequireSignature, _options.WebhookSecret, cancellationToken);
    }

    private async Task<IActionResult> ReceiveAsync(
        string provider,
        string signatureHeader,
        bool requireSignature,
        string? secret,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
            return BadRequestValidationApi(new ValidationError("body", "Empty webhook payload."));

        if (requireSignature)
        {
            if (!Request.Headers.TryGetValue(signatureHeader, out var sigValues))
                return BadRequestValidationApi(new ValidationError(signatureHeader,
                    $"Header is required when {provider} webhook secret is configured."));

            if (!HmacWebhookSignatureVerifier.TryVerify(payload, sigValues.ToString(), secret!))
                return FailApi(new ForbiddenError($"Invalid {provider} webhook signature."));
        }

        string? eventId = null;
        string? eventType = null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                eventId = idEl.GetString();
            else if (doc.RootElement.TryGetProperty("orderId", out var orderEl) &&
                     orderEl.ValueKind == JsonValueKind.String)
                eventId = orderEl.GetString();

            if (doc.RootElement.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                eventType = typeEl.GetString();
            else if (doc.RootElement.TryGetProperty("event_type", out var etEl) &&
                     etEl.ValueKind == JsonValueKind.String)
                eventType = etEl.GetString();
            else if (doc.RootElement.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.String)
                eventType = codeEl.GetString();
        }
        catch (JsonException)
        {
            return BadRequestValidationApi(new ValidationError("body", "Invalid JSON payload."));
        }

        var result = await Sender.Send(
            new ReceivePaymentWebhookCommand(provider, payload, eventId, eventType ?? "payment.success"),
            cancellationToken);

        return MatchApi(
            result,
            dto => OkApi(new PaymentWebhookReceiptResponse(dto.EventId, dto.Duplicate, dto.Status)));
    }
}

[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/webhooks/payos")]
public sealed class PayOsWebhooksController : ApiControllerBase
{
    private readonly PayOsWebhookOptions _options;

    public PayOsWebhooksController(ISender sender, IOptions<PayOsWebhookOptions> options) : base(sender)
    {
        _options = options.Value;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PaymentWebhookReceiptResponse>), StatusCodes.Status200OK)]
    public Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        return ReceiveAsync("payos", "X-PayOS-Signature", _options.RequireSignature, _options.WebhookSecret, cancellationToken);
    }

    private async Task<IActionResult> ReceiveAsync(
        string provider,
        string signatureHeader,
        bool requireSignature,
        string? secret,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
            return BadRequestValidationApi(new ValidationError("body", "Empty webhook payload."));

        if (requireSignature)
        {
            if (!Request.Headers.TryGetValue(signatureHeader, out var sigValues))
                return BadRequestValidationApi(new ValidationError(signatureHeader,
                    $"Header is required when {provider} webhook secret is configured."));

            if (!HmacWebhookSignatureVerifier.TryVerify(payload, sigValues.ToString(), secret!))
                return FailApi(new ForbiddenError($"Invalid {provider} webhook signature."));
        }

        string? eventId = null;
        string? eventType = null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                eventId = idEl.GetString();
            else if (doc.RootElement.TryGetProperty("orderId", out var orderEl) &&
                     orderEl.ValueKind == JsonValueKind.String)
                eventId = orderEl.GetString();

            if (doc.RootElement.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                eventType = typeEl.GetString();
            else if (doc.RootElement.TryGetProperty("event_type", out var etEl) &&
                     etEl.ValueKind == JsonValueKind.String)
                eventType = etEl.GetString();
            else if (doc.RootElement.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.String)
                eventType = codeEl.GetString();
        }
        catch (JsonException)
        {
            return BadRequestValidationApi(new ValidationError("body", "Invalid JSON payload."));
        }

        var result = await Sender.Send(
            new ReceivePaymentWebhookCommand(provider, payload, eventId, eventType ?? "payment.success"),
            cancellationToken);

        return MatchApi(
            result,
            dto => OkApi(new PaymentWebhookReceiptResponse(dto.EventId, dto.Duplicate, dto.Status)));
    }
}
