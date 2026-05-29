using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Modules.Users.Persistence.Conversions;

/// <summary>
///     Process-wide accessor for the <see cref="IDataProtectionProvider" /> used by user-aggregate
///     value converters. Initialised once during <c>AddInfrastructure</c>; only invoked during EF
///     <c>OnModelCreating</c> and value-converter execution.
/// </summary>
internal static class UserDataProtection
{
    private static IDataProtectionProvider? _provider;

    public static IDataProtectionProvider Provider =>
        _provider ?? throw new InvalidOperationException(
            "UserDataProtection.Initialize must be called before EF model is built.");

    public static void Initialize(IDataProtectionProvider provider)
    {
        _provider = provider;
    }
}