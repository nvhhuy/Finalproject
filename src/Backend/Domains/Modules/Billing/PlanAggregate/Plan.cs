using Domains.Common;

namespace Domains.Billing;

public sealed class Plan : AggregateRoot<Guid>
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public int PriceMonthlyCents { get; private set; }
    public string Currency { get; private set; } = "USD";
    public int TrialDays { get; private set; }
    public string LimitsJson { get; private set; } = "{}";
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Plan() { }

    private Plan(
        Guid id,
        string code,
        string name,
        string? description,
        int priceMonthlyCents,
        string currency,
        int trialDays,
        string limitsJson,
        bool isActive,
        int sortOrder,
        DateTime utcNow) : base(id)
    {
        Code = code;
        Name = name;
        Description = description;
        PriceMonthlyCents = priceMonthlyCents;
        Currency = currency;
        TrialDays = trialDays;
        LimitsJson = limitsJson;
        IsActive = isActive;
        SortOrder = sortOrder;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public static Plan Create(
        string code,
        string name,
        string? description,
        int priceMonthlyCents,
        string currency,
        int trialDays,
        string limitsJson,
        int sortOrder,
        DateTime utcNow)
    {
        string normalizedCode = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Plan name is required.", nameof(name));
        if (priceMonthlyCents < 0)
            throw new ArgumentOutOfRangeException(nameof(priceMonthlyCents));
        if (trialDays < 0)
            throw new ArgumentOutOfRangeException(nameof(trialDays));

        return new Plan(
            Guid.NewGuid(),
            normalizedCode,
            name.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            priceMonthlyCents,
            string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant(),
            trialDays,
            string.IsNullOrWhiteSpace(limitsJson) ? "{}" : limitsJson,
            isActive: true,
            sortOrder,
            utcNow);
    }

    public void Update(
        string name,
        string? description,
        int priceMonthlyCents,
        string currency,
        int trialDays,
        string limitsJson,
        bool isActive,
        int sortOrder,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Plan name is required.", nameof(name));
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        PriceMonthlyCents = priceMonthlyCents;
        Currency = string.IsNullOrWhiteSpace(currency) ? Currency : currency.Trim().ToUpperInvariant();
        TrialDays = trialDays;
        LimitsJson = string.IsNullOrWhiteSpace(limitsJson) ? "{}" : limitsJson;
        IsActive = isActive;
        SortOrder = sortOrder;
        UpdatedAtUtc = utcNow;
    }

    public static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Plan code is required.", nameof(code));
        return code.Trim().ToLowerInvariant();
    }
}
