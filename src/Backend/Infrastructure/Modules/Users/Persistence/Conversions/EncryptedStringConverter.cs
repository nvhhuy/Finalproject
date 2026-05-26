using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Modules.Users.Persistence.Conversions;

/// <summary>
/// EF Core value converter that transparently encrypts a string column at rest using
/// ASP.NET <see cref="IDataProtector"/>. Round-trip: domain holds plaintext, DB stores the
/// Base64URL ciphertext. Existing plaintext rows (pre-encryption) will fail to decrypt;
/// users must re-enrol 2FA after migration.
/// </summary>
/// <remarks>
/// Decryption failures are surfaced as <see cref="EncryptedColumnDecryptionException"/> so that
/// a corrupted/tampered ciphertext or a key rotation gone wrong cannot silently degrade the
/// system to "value missing". Callers (or the global exception handler) can decide whether to
/// 5xx the request or trigger an operator alert.
/// </remarks>
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
        catch (Exception ex)
        {
            throw new EncryptedColumnDecryptionException(
                "Failed to decrypt an encrypted column. The ciphertext may be tampered with, the data-protection key may have been rotated without a recovery path, or the column may contain legacy plaintext that was never encrypted.",
                ex);
        }
    }
}

/// <summary>Thrown by <see cref="EncryptedStringConverter"/> when a value cannot be decrypted.</summary>
public sealed class EncryptedColumnDecryptionException : Exception
{
    public EncryptedColumnDecryptionException(string message, Exception inner) : base(message, inner) { }
}
