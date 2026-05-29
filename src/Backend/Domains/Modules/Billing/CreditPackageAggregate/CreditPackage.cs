using Domains.Common;

namespace Domains.Billing;

/// <summary>Top-up SKU: maps money (smallest currency unit) to wallet credits.</summary>
public sealed class CreditPackage : AggregateRoot<Guid>
{
    private CreditPackage()
    {
    }

    private CreditPackage(
        Guid id,
        string code,
        string name,
        string? description,
        int priceCents,
        string currency,
        long creditsAmount,
        bool isActive,
        int sortOrder,
        DateTime utcNow) : base(id)
    {
        Code = code;
        Name = name;
        Description = description;
        PriceCents = priceCents;
        Currency = currency;
        CreditsAmount = creditsAmount;
        IsActive = isActive;
        SortOrder = sortOrder;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    /// <summary>Price in smallest currency unit (USD cents, VND đồng, …).</summary>
    public int PriceCents { get; private set; }

    public string Currency { get; private set; } = "USD";
    public long CreditsAmount { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static CreditPackage Create(
        string code,
        string name,
        string? description,
        int priceCents,
        string currency,
        long creditsAmount,
        int sortOrder,
        DateTime utcNow)
    {
        var normalizedCode = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Package name is required.", nameof(name));
        if (priceCents < 0)
            throw new ArgumentOutOfRangeException(nameof(priceCents));
        if (creditsAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(creditsAmount), "Credits amount must be positive.");

        return new CreditPackage(
            Guid.NewGuid(),
            normalizedCode,
            name.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            priceCents,
            string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant(),
            creditsAmount,
            true,
            sortOrder,
            utcNow);
    }

    public void Update(
        string name,
        string? description,
        int priceCents,
        string currency,
        long creditsAmount,
        bool isActive,
        int sortOrder,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Package name is required.", nameof(name));
        if (priceCents < 0)
            throw new ArgumentOutOfRangeException(nameof(priceCents));
        if (creditsAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(creditsAmount));

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        PriceCents = priceCents;
        Currency = string.IsNullOrWhiteSpace(currency) ? Currency : currency.Trim().ToUpperInvariant();
        CreditsAmount = creditsAmount;
        IsActive = isActive;
        SortOrder = sortOrder;
        UpdatedAtUtc = utcNow;
    }

    public static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Package code is required.", nameof(code));
        return code.Trim().ToLowerInvariant();
    }
}