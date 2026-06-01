using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Modules.Billing;

/// <summary>
///     Verifies a generic HMAC-SHA256 webhook signature header.
///     Expected header format: the raw hex-encoded HMAC-SHA256 of the request body bytes.
///     Used by VNPay (<c>X-VNPay-Signature</c>) and PayOS (<c>X-PayOS-Signature</c>).
/// </summary>
public static class HmacWebhookSignatureVerifier
{
    /// <summary>
    ///     Returns <c>true</c> when <paramref name="signatureHeader" /> matches the HMAC-SHA256 of
    ///     <paramref name="payload" /> keyed with <paramref name="secret" />.
    ///     Comparison is timing-safe via <see cref="CryptographicOperations.FixedTimeEquals" />.
    /// </summary>
    public static bool TryVerify(string payload, string signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(payload)
            || string.IsNullOrWhiteSpace(signatureHeader)
            || string.IsNullOrWhiteSpace(secret))
            return false;

        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(secretBytes);
        var expected = Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
        var provided = signatureHeader.Trim().ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided));
    }
}
