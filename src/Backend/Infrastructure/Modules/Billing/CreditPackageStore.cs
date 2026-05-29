using Application.Contracts.Billing;
using Domains.Billing;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Billing;

public sealed class CreditPackageStore : ICreditPackageStore
{
    private readonly ApplicationDbContext _db;

    public CreditPackageStore(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken = default)
    {
        return _db.CreditPackages.AsNoTracking().AnyAsync(p => p.Code == normalizedCode, cancellationToken);
    }

    public Task<CreditPackage?> GetByCodeAsync(string normalizedCode, bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var q = _db.CreditPackages.Where(p => p.Code == normalizedCode);
        if (activeOnly)
            q = q.Where(p => p.IsActive);
        return q.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<CreditPackage?> GetByIdForUpdateAsync(Guid packageId, CancellationToken cancellationToken = default)
    {
        return _db.CreditPackages.FirstOrDefaultAsync(p => p.Id == packageId, cancellationToken);
    }

    public void Add(CreditPackage package)
    {
        _db.CreditPackages.Add(package);
    }

    public void Remove(CreditPackage package)
    {
        _db.CreditPackages.Remove(package);
    }
}