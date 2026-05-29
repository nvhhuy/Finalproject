using Infrastructure.Modules.Users.Persistence.Conversions;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Tests.Persistence;

public sealed class EncryptedStringConverterTests
{
    [Fact]
    public void Roundtrip_returns_original_plaintext()
    {
        var protector = NewProtector("test.purpose.v1");
        var converter = new EncryptedStringConverter(protector);

        var ciphertext = (string)converter.ConvertToProvider("plain-base32-secret")!;
        var plaintext = (string)converter.ConvertFromProvider(ciphertext)!;

        Assert.NotEqual("plain-base32-secret", ciphertext);
        Assert.Equal("plain-base32-secret", plaintext);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Null_or_empty_passes_through_unchanged(string? input)
    {
        var converter = new EncryptedStringConverter(NewProtector("test"));

        var encoded = converter.ConvertToProvider(input);
        var decoded = converter.ConvertFromProvider(input);

        Assert.Equal(input, encoded);
        Assert.Equal(input, decoded);
    }

    [Fact]
    public void Different_purposes_cannot_decrypt_each_other()
    {
        IDataProtectionProvider provider = new EphemeralDataProtectionProvider();
        var encoder = new EncryptedStringConverter(provider.CreateProtector("purposeA"));
        var decoder = new EncryptedStringConverter(provider.CreateProtector("purposeB"));

        var ciphertext = (string)encoder.ConvertToProvider("topsecret")!;

        Assert.Throws<EncryptedColumnDecryptionException>(() => decoder.ConvertFromProvider(ciphertext));
    }

    [Fact]
    public void Tampered_ciphertext_surfaces_as_typed_exception_not_silent_null()
    {
        var converter = new EncryptedStringConverter(NewProtector("p"));

        var ex = Assert.Throws<EncryptedColumnDecryptionException>(() =>
            converter.ConvertFromProvider("not-a-real-protected-payload"));

        Assert.NotNull(ex.InnerException);
    }

    private static IDataProtector NewProtector(string purpose)
    {
        return new EphemeralDataProtectionProvider().CreateProtector(purpose);
    }
}