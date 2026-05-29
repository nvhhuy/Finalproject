using Application.Contracts.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Tenancy;
using Application.Contracts.Transactions;
using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Application.Modules.Tenancy;

public sealed record TenantHostResolutionDto(
    Guid TenantId,
    string Slug,
    string DisplayName,
    string Source);

public sealed record ResolveTenantByHostQuery(string Host) : IRequest<Result<TenantHostResolutionDto, Error>>;

public sealed class
    ResolveTenantByHostQueryHandler : IRequestHandler<ResolveTenantByHostQuery, Result<TenantHostResolutionDto, Error>>
{
    private readonly IApplicationReadDbContext _db;
    private readonly ITenantHostResolver _resolver;

    public ResolveTenantByHostQueryHandler(ITenantHostResolver resolver, IApplicationReadDbContext db)
    {
        _resolver = resolver;
        _db = db;
    }

    public async Task<Result<TenantHostResolutionDto, Error>> Handle(
        ResolveTenantByHostQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Host))
            return new ValidationError(nameof(request.Host), "Host is required.");

        var resolution = await _resolver.ResolveAsync(request.Host, cancellationToken);
        if (resolution is null)
            return new NotFoundError("No tenant is mapped to this host.");

        var tenant = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == TenantId.Create(resolution.TenantId) && t.Status == TenantStatus.Active)
            .Select(t => new TenantPublicDto(
                t.Id.Value,
                t.Slug,
                t.DisplayName,
                t.PublicTagline,
                t.PublicDescription))
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null)
            return new NotFoundError("Tenant was not found.");

        return new TenantHostResolutionDto(
            tenant.Id,
            tenant.Slug,
            tenant.DisplayName,
            resolution.Source.ToString());
    }
}

public sealed record TenantDomainDto(
    Guid Id,
    Guid TenantId,
    string Hostname,
    string Status,
    string VerificationToken,
    string TxtRecordName,
    string TxtRecordValue,
    string? CnameTarget,
    DateTime CreatedAtUtc,
    DateTime? VerifiedAtUtc);

public sealed record ListTenantDomainsQuery(Guid TenantId, Guid UserId)
    : IRequest<Result<IReadOnlyList<TenantDomainDto>, Error>>;

public sealed class
    ListTenantDomainsQueryHandler : IRequestHandler<ListTenantDomainsQuery,
    Result<IReadOnlyList<TenantDomainDto>, Error>>
{
    private readonly IApplicationReadDbContext _db;
    private readonly ITenantMembershipService _membership;
    private readonly IOptions<TenancyHostOptions> _options;

    public ListTenantDomainsQueryHandler(
        IApplicationReadDbContext db,
        ITenantMembershipService membership,
        IOptions<TenancyHostOptions> options)
    {
        _db = db;
        _membership = membership;
        _options = options;
    }

    public async Task<Result<IReadOnlyList<TenantDomainDto>, Error>> Handle(
        ListTenantDomainsQuery request,
        CancellationToken cancellationToken)
    {
        if (!await _membership.IsMemberAsync(request.TenantId, request.UserId, cancellationToken))
            return new ForbiddenError("You are not a member of this tenant.");

        var hostOptions = _options.Value;
        var tenantKey = TenantId.Create(request.TenantId);
        var rows = await _db.TenantDomains
            .AsNoTracking()
            .Where(d => d.TenantId == tenantKey && d.Status != TenantDomainStatus.Disabled)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(d => ToDto(d, hostOptions)).ToList();
    }

    internal static TenantDomainDto ToDto(TenantDomain domain, TenancyHostOptions options)
    {
        var prefix = options.VerificationTxtPrefix.Trim('.');
        return new TenantDomainDto(
            domain.Id,
            domain.TenantId.Value,
            domain.Hostname,
            domain.Status.ToString(),
            domain.VerificationToken,
            $"{prefix}.{domain.Hostname}",
            domain.VerificationToken,
            options.PlatformCnameTarget,
            domain.CreatedAtUtc,
            domain.VerifiedAtUtc);
    }
}

public sealed record AddTenantDomainCommand(Guid TenantId, Guid UserId, string Hostname)
    : IRequest<Result<TenantDomainDto, Error>>;

public sealed class AddTenantDomainCommandValidator : AbstractValidator<AddTenantDomainCommand>
{
    public AddTenantDomainCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Hostname).NotEmpty().MaximumLength(253);
    }
}

public sealed class
    AddTenantDomainCommandHandler : IRequestHandler<AddTenantDomainCommand, Result<TenantDomainDto, Error>>
{
    private readonly IDateTimeProvider _clock;
    private readonly ITenantDomainStore _domains;
    private readonly ITenantMembershipService _membership;
    private readonly IOptions<TenancyHostOptions> _options;
    private readonly IUnitOfWork _uow;

    public AddTenantDomainCommandHandler(
        ITenantDomainStore domains,
        ITenantMembershipService membership,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        IOptions<TenancyHostOptions> options)
    {
        _domains = domains;
        _membership = membership;
        _uow = uow;
        _clock = clock;
        _options = options;
    }

    public async Task<Result<TenantDomainDto, Error>> Handle(
        AddTenantDomainCommand request,
        CancellationToken cancellationToken)
    {
        if (!await _membership.IsOwnerAsync(request.TenantId, request.UserId, cancellationToken))
            return new ForbiddenError("Only the tenant owner can manage custom domains.");

        string hostname;
        try
        {
            hostname = TenantDomain.NormalizeHostname(request.Hostname);
        }
        catch (ArgumentException ex)
        {
            return new ValidationError(nameof(request.Hostname), ex.Message);
        }

        var hostOptions = _options.Value;
        if (!string.IsNullOrWhiteSpace(hostOptions.SubdomainBaseHost)
            && (hostname == hostOptions.SubdomainBaseHost.Trim().ToLowerInvariant()
                || hostname.EndsWith($".{hostOptions.SubdomainBaseHost.Trim().ToLowerInvariant()}",
                    StringComparison.Ordinal)))
            return new ValidationError(nameof(request.Hostname),
                "Use your tenant subdomain instead of the platform base host.");

        if (await _domains.HostnameExistsAsync(hostname, cancellationToken))
            return new ValidationError(nameof(request.Hostname), "This hostname is already registered.");

        var tenantKey = TenantId.Create(request.TenantId);
        var domain = TenantDomain.Create(
            tenantKey,
            hostname,
            TenantDomain.CreateVerificationToken(),
            _clock.Now);

        _domains.Add(domain);
        await _uow.SaveChangesAsync(cancellationToken);
        return ListTenantDomainsQueryHandler.ToDto(domain, hostOptions);
    }
}

public sealed record VerifyTenantDomainCommand(Guid TenantId, Guid UserId, Guid DomainId)
    : IRequest<Result<TenantDomainDto, Error>>;

public sealed class
    VerifyTenantDomainCommandHandler : IRequestHandler<VerifyTenantDomainCommand, Result<TenantDomainDto, Error>>
{
    private readonly IDateTimeProvider _clock;
    private readonly ITenantDomainDnsVerifier _dnsVerifier;
    private readonly ITenantDomainStore _domains;
    private readonly ITenantMembershipService _membership;
    private readonly IOptions<TenancyHostOptions> _options;
    private readonly IUnitOfWork _uow;

    public VerifyTenantDomainCommandHandler(
        ITenantDomainStore domains,
        ITenantMembershipService membership,
        ITenantDomainDnsVerifier dnsVerifier,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        IOptions<TenancyHostOptions> options)
    {
        _domains = domains;
        _membership = membership;
        _dnsVerifier = dnsVerifier;
        _uow = uow;
        _clock = clock;
        _options = options;
    }

    public async Task<Result<TenantDomainDto, Error>> Handle(
        VerifyTenantDomainCommand request,
        CancellationToken cancellationToken)
    {
        if (!await _membership.IsOwnerAsync(request.TenantId, request.UserId, cancellationToken))
            return new ForbiddenError("Only the tenant owner can verify custom domains.");

        var tenantKey = TenantId.Create(request.TenantId);
        var domain = await _domains.GetForUpdateAsync(request.DomainId, tenantKey, cancellationToken);
        if (domain is null)
            return new NotFoundError("Domain was not found.");
        if (domain.Status == TenantDomainStatus.Disabled)
            return new ValidationError("domain", "Domain is disabled.");

        var verified = await _dnsVerifier.HasVerificationTxtRecordAsync(
            domain.Hostname,
            domain.VerificationToken,
            cancellationToken);
        if (!verified)
        {
            var opts = _options.Value;
            var prefix = opts.VerificationTxtPrefix.Trim('.');
            return new ValidationError(
                "dns",
                $"Add TXT record {prefix}.{domain.Hostname} = {domain.VerificationToken} (or CNAME to {opts.PlatformCnameTarget ?? "your platform host"}) then retry.");
        }

        domain.MarkVerified(_clock.Now);
        domain.Activate(_clock.Now);
        await _uow.SaveChangesAsync(cancellationToken);
        return ListTenantDomainsQueryHandler.ToDto(domain, _options.Value);
    }
}

public sealed record RemoveTenantDomainCommand(Guid TenantId, Guid UserId, Guid DomainId)
    : IRequest<Result<Unit, Error>>;

public sealed class RemoveTenantDomainCommandHandler : IRequestHandler<RemoveTenantDomainCommand, Result<Unit, Error>>
{
    private readonly ITenantDomainStore _domains;
    private readonly ITenantMembershipService _membership;
    private readonly IUnitOfWork _uow;

    public RemoveTenantDomainCommandHandler(
        ITenantDomainStore domains,
        ITenantMembershipService membership,
        IUnitOfWork uow)
    {
        _domains = domains;
        _membership = membership;
        _uow = uow;
    }

    public async Task<Result<Unit, Error>> Handle(
        RemoveTenantDomainCommand request,
        CancellationToken cancellationToken)
    {
        if (!await _membership.IsOwnerAsync(request.TenantId, request.UserId, cancellationToken))
            return new ForbiddenError("Only the tenant owner can remove custom domains.");

        var tenantKey = TenantId.Create(request.TenantId);
        var domain = await _domains.GetForUpdateAsync(request.DomainId, tenantKey, cancellationToken);
        if (domain is null)
            return new NotFoundError("Domain was not found.");

        domain.Disable();
        await _uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}