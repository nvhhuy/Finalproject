

namespace Infrastructure.Authentication
{
    public class JwtSettings
    {
        public const string SectionName = "JwtSettings";
        public string Secret { get; init; } = null!;
        public int ExpirationTimeInMinutes { get; init; }
        /// <summary>Sliding refresh token lifetime.</summary>
        public int RefreshTokenExpirationDays { get; init; } = 14;
        public string Issuer { get; init; } = null!;
        public string Audience { get; init; } = null!;

        /// <summary>Dev-only signing key shipped in appsettings.Development.json.</summary>
        /// <remarks>
        /// Production deployments MUST override via user-secrets / environment variable / KMS-
        /// resolved config provider. Refusing to boot when this exact value is seen outside
        /// Development closes the "copied dev settings into prod" footgun.
        /// </remarks>
        public const string DevelopmentSecretSentinel = "DevelopmentSigningKey_ChangeMe_UseLongSecretAtLeast32Chars!";

        /// <summary>Fail fast on weak or missing JWT config (call at startup from DI).</summary>
        public static void EnsureValidForSigning(JwtSettings settings, bool isDevelopment = false)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (string.IsNullOrWhiteSpace(settings.Issuer))
                throw new InvalidOperationException($"{SectionName}:Issuer is required.");
            if (string.IsNullOrWhiteSpace(settings.Audience))
                throw new InvalidOperationException($"{SectionName}:Audience is required.");
            if (string.IsNullOrWhiteSpace(settings.Secret))
                throw new InvalidOperationException($"{SectionName}:Secret is required.");
            // HS256: use at least 256 bits (32 chars) of entropy in production configs.
            if (settings.Secret.Length < 32)
                throw new InvalidOperationException($"{SectionName}:Secret must be at least 32 characters for HS256.");
            if (!isDevelopment && string.Equals(settings.Secret, DevelopmentSecretSentinel, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:Secret still uses the development sentinel value. Override via " +
                    "user-secrets (`dotnet user-secrets set \"JwtSettings:Secret\" <value>`) or a " +
                    "production secret manager. Refusing to start outside Development with the dev key.");
            }
            if (settings.ExpirationTimeInMinutes <= 0)
                throw new InvalidOperationException($"{SectionName}:ExpirationTimeInMinutes must be positive.");
            if (settings.RefreshTokenExpirationDays <= 0)
                throw new InvalidOperationException($"{SectionName}:RefreshTokenExpirationDays must be positive.");
        }
    }
}
