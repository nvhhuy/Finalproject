using System.Security.Cryptography;
using System.Text;
using Infrastructure.Modules.Billing;

namespace Infrastructure.Tests.Billing;

public sealed class HmacWebhookSignatureVerifierTests
{
    private const string Secret = "super-secret-key";
    private const string Payload = "{\"event\":\"payment.completed\",\"amount\":10000}";

    private static string ComputeSignature(string payload, string secret)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(secretBytes);
        return Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
    }

    [Fact]
    public void TryVerify_returns_true_for_valid_signature()
    {
        var signature = ComputeSignature(Payload, Secret);

        var result = HmacWebhookSignatureVerifier.TryVerify(Payload, signature, Secret);

        Assert.True(result);
    }

    [Fact]
    public void TryVerify_returns_true_for_uppercase_signature()
    {
        var signature = ComputeSignature(Payload, Secret).ToUpperInvariant();

        var result = HmacWebhookSignatureVerifier.TryVerify(Payload, signature, Secret);

        Assert.True(result);
    }

    [Fact]
    public void TryVerify_returns_true_for_signature_with_leading_and_trailing_whitespace()
    {
        var signature = "  " + ComputeSignature(Payload, Secret) + "  ";

        var result = HmacWebhookSignatureVerifier.TryVerify(Payload, signature, Secret);

        Assert.True(result);
    }

    [Fact]
    public void TryVerify_returns_false_for_wrong_secret()
    {
        var signature = ComputeSignature(Payload, "wrong-secret");

        var result = HmacWebhookSignatureVerifier.TryVerify(Payload, signature, Secret);

        Assert.False(result);
    }

    [Fact]
    public void TryVerify_returns_false_for_tampered_payload()
    {
        var signature = ComputeSignature(Payload, Secret);
        var tamperedPayload = Payload.Replace("10000", "1");

        var result = HmacWebhookSignatureVerifier.TryVerify(tamperedPayload, signature, Secret);

        Assert.False(result);
    }

    [Fact]
    public void TryVerify_returns_false_for_garbage_signature()
    {
        var result = HmacWebhookSignatureVerifier.TryVerify(Payload, "not-a-real-hash", Secret);

        Assert.False(result);
    }

    [Theory]
    [InlineData("", "somesig", "secret")]
    [InlineData("   ", "somesig", "secret")]
    [InlineData("payload", "", "secret")]
    [InlineData("payload", "   ", "secret")]
    [InlineData("payload", "somesig", "")]
    [InlineData("payload", "somesig", "   ")]
    public void TryVerify_returns_false_when_any_argument_is_null_or_whitespace(
        string payload, string signature, string secret)
    {
        var result = HmacWebhookSignatureVerifier.TryVerify(payload, signature, secret);

        Assert.False(result);
    }

    [Fact]
    public void TryVerify_is_deterministic_same_inputs_always_return_true()
    {
        var signature = ComputeSignature(Payload, Secret);

        for (var i = 0; i < 10; i++)
            Assert.True(HmacWebhookSignatureVerifier.TryVerify(Payload, signature, Secret));
    }

    [Fact]
    public void TryVerify_different_payloads_produce_different_signatures()
    {
        var sig1 = ComputeSignature("payload-a", Secret);
        var sig2 = ComputeSignature("payload-b", Secret);

        Assert.NotEqual(sig1, sig2);
        Assert.False(HmacWebhookSignatureVerifier.TryVerify("payload-a", sig2, Secret));
        Assert.False(HmacWebhookSignatureVerifier.TryVerify("payload-b", sig1, Secret));
    }
}
