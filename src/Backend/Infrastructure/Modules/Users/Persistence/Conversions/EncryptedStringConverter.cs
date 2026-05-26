using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Modules.Users.Persistence.Conversions;

/// <summary>
/// EF Core value converter that transparently encrypts a string column at rest using
/// ASP.NET <see cref="IDataProtector"/>. Round-trip: domain holds plaintext, DB stores the
/// Base64URL ciphertext. Existing plaintext rows (pre-encryption) will fail to decrypt;
/// users must re-enrol 2FA after migration.
/// </summary>
public sealed class EncryptedStringConverter : ValueConverter<string?, string?>
{
    public EncryptedStringConverter(IDataProtector protector)
        : base(
            plaintext => Encrypt(protector, plaintext),
            ciphertext => Decrypt(protector, ciphertext))
    {
    }

    private static string? Encrypt(IDataProtector protector, string? plaintext)
        => string.IsNullOrEmpty(plaintext) ? plaintext : protector.Protect(plaintext);

    private static string? Decrypt(IDataProtector protector, string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return ciphertext;
        try
        {
            return protector.Unprotect(ciphertext);
        }
        catch
        {
            return null;
        }
    }
}
