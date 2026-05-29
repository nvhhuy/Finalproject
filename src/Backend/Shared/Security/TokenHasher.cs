using System.Security.Cryptography;
using System.Text;

namespace Shared.Security;

public static class TokenHasher
{
    public static string Sha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }
}