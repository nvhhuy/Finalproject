using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Modules.Billing;

/// <summary>Verifies Stripe <c>Stripe-Signature</c> header (HMAC SHA256).</summary>
public static class StripeWebhookSignatureVerifier
{
    public static bool TryVerify(string payload, string signatureHeader, string webhookSecret, TimeSpan tolerance)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(signatureHeader) ||
            string.IsNullOrWhiteSpace(webhookSecret))
            return false;

        long? timestamp = null;
        string? signature = null;
        foreach (var part in signatureHeader.Split(',',
                     StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            var key = part[..eq];
            var value = part[(eq + 1)..];
            if (key == "t" && long.TryParse(value, out var ts))
                timestamp = ts;
            else if (key == "v1")
                signature = value;
        }

        if (timestamp is null || string.IsNullOrEmpty(signature))
            return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp.Value) > (long)tolerance.TotalSeconds)
            return false;

        var signedPayload = $"{timestamp.Value}.{payload}";
        var secretBytes = Encoding.UTF8.GetBytes(webhookSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(signedPayload);
        using var hmac = new HMACSHA256(secretBytes);
        var expected = Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
    }
}