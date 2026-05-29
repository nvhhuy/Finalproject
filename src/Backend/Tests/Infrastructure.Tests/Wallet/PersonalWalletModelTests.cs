using Domains.Wallet;
using Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Infrastructure.Tests.Wallet;

public sealed class PersonalWalletModelTests
{
    [Fact]
    public void PersonalWallet_row_version_is_not_a_concurrency_token()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        var entity = db.Model.FindEntityType(typeof(PersonalWallet))!;
        var rowVersion = entity.FindProperty(nameof(PersonalWallet.RowVersion))!;
        Assert.False(rowVersion.IsConcurrencyToken);
    }

    [Fact]
    public void WalletLedgerEntry_row_version_is_not_a_concurrency_token()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        var entity = db.Model.FindEntityType(typeof(WalletLedgerEntry))!;
        var rowVersion = entity.FindProperty(nameof(WalletLedgerEntry.RowVersion))!;
        Assert.False(rowVersion.IsConcurrencyToken);
    }

    [Fact]
    public void WalletLedgerEntry_id_is_client_generated()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        var id = db.Model.FindEntityType(typeof(WalletLedgerEntry))!.FindProperty(nameof(WalletLedgerEntry.Id))!;
        Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
    }
}