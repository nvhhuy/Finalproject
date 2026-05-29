using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Wallet;

internal static class WalletDbExceptionHelper
{
    public const string LedgerIdempotencyIndex = "IX_WalletLedgerEntries_IdempotencyKey";
    public const string WalletUserUniqueIndex = "IX_PersonalWallets_UserId";

    public static bool IsUniqueViolation(DbUpdateException ex, string indexName) =>
        ex.InnerException?.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase) == true
        || ex.Entries.Any(e => e.Metadata.GetIndexes()
            .Any(i => i.Name?.Equals(indexName, StringComparison.OrdinalIgnoreCase) == true));
}
