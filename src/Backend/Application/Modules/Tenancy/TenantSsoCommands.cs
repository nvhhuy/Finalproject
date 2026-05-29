using Application.Contracts.ExternalAuth;
using Application.Contracts.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Tenancy;
using Application.Contracts.Transactions;
using Application.Modules.Users.Authentication.Commands.TenantOidc;
using Application.Modules.Users.Authentication.Common;
using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Shared.Errors.Authentication;

namespace Application.Modules.Tenancy;

public sealed record TenantSsoSettingsDto(
    Guid Id,
    Guid TenantId,
    bool IsEnabled,
    string OidcAuthority,
    string OidcClientId,
    bool HasClientSecret,
    DateTime UpdatedAtUtc)
{
    public static TenantSsoSettingsDto From(TenantSsoSettings settings)
    {
        return new TenantSsoSettingsDto(
            settings.Id,
            settings.TenantId.Value,
            settings.IsEnabled,
            settings.OidcAuthority,
            settings.OidcClientId,
            !string.IsNullOrWhiteSpace(settings.OidcClientSecret),
            settings.UpdatedAtUtc);
    }
}

public sealed record TenantSsoPublicDto(
    Guid TenantId,
    string TenantSlug,
    bool IsEnabled,
    string OidcAuthority,
    string OidcClientId);

public sealed record GetTenantSsoSettingsQuery(Guid TenantId, Guid UserId)
    : IRequest<Result<TenantSsoSettingsDto, Error>>;

public sealed class
    GetTenantSsoSettingsQueryHandler : IRequestHandler<GetTenantSsoSettingsQuery, Result<TenantSsoSettingsDto, Error>>
{
    private readonly IApplicationReadDbContext _db;
    private readonly ITenantMembershipService _membership;

    public GetTenantSsoSettingsQueryHandler(IApplicationReadDbContext db, ITenantMembershipService membership)
    {
        _db = db;
        _membership = membership;
    }

    public async Task<Result<TenantSsoSettingsDto, Error>> Handle(
        GetTenantSsoSettingsQuery request,
        CancellationToken cancellationToken)
    {
        if (!await _membership.IsOwnerAsync(request.TenantId, request.UserId, cancellationToken))
            return new ForbiddenError("Only the tenant owner can view SSO settings.");

        var tenantKey = TenantId.Create(request.TenantId);
        var settings = await _db.TenantSsoSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantKey, cancellationToken);
        if (settings is null)
            return new NotFoundError("SSO is not configured for this tenant.");

        return TenantSsoSettingsDto.From(settings);
    }
}

public sealed record GetPublicTenantSsoBySlugQuery(string Slug) : IRequest<Result<TenantSsoPublicDto, Error>>;

public sealed class GetPublicTenantSsoBySlugQueryHandler
    : IRequestHandler<GetPublicTenantSsoBySlugQuery, Result<TenantSsoPublicDto, Error>>
{
    private readonly IApplicationReadDbContext _db;

    public GetPublicTenantSsoBySlugQueryHandler(IApplicationReadDbContext db)
    {
        _db = db;
    }

    public async Task<Result<TenantSsoPublicDto, Error>> Handle(
        GetPublicTenantSsoBySlugQuery request,
        CancellationToken cancellationToken)
    {
        string slug;
        try
        {
            slug = Tenant.NormalizeSlug(request.Slug);
        }
        catch (ArgumentException ex)
        {
            return new ValidationError(nameof(request.Slug), ex.Message);
        }

        var row = await (
                from t in _db.Tenants.AsNoTracking()
                join s in _db.TenantSsoSettings.AsNoTracking() on t.Id equals s.TenantId
                where t.Slug == slug && t.Status == TenantStatus.Active && s.IsEnabled
                select new { t.Id, t.Slug, s.OidcAuthority, s.OidcClientId })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return new NotFoundError("Tenant SSO is not available.");

        return new TenantSsoPublicDto(row.Id.Value, row.Slug, true, row.OidcAuthority, row.OidcClientId);
    }
}

public sealed record UpsertTenantSsoSettingsCommand(
    Guid TenantId,
    Guid UserId,
    bool IsEnabled,
    string OidcAuthority,
    string OidcClientId,
    string? OidcClientSecret) : IRequest<Result<TenantSsoSettingsDto, Error>>;

public sealed class UpsertTenantSsoSettingsCommandValidator : AbstractValidator<UpsertTenantSsoSettingsCommand>
{
    public UpsertTenantSsoSettingsCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.OidcAuthority).NotEmpty().MaximumLength(500);
        RuleFor(x => x.OidcClientId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OidcClientSecret).MaximumLength(500);
    }
}

public sealed class UpsertTenantSsoSettingsCommandHandler
    : IRequestHandler<UpsertTenantSsoSettingsCommand, Result<TenantSsoSettingsDto, Error>>
{
    private readonly IDateTimeProvider _clock;
    private readonly ITenantMembershipService _membership;
    private readonly ITenantSsoSettingsStore _store;
    private readonly IUnitOfWork _uow;

    public UpsertTenantSsoSettingsCommandHandler(
        ITenantSsoSettingsStore store,
        ITenantMembershipService membership,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _store = store;
        _membership = membership;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<TenantSsoSettingsDto, Error>> Handle(
        UpsertTenantSsoSettingsCommand request,
        CancellationToken cancellationToken)
    {
        if (!await _membership.IsOwnerAsync(request.TenantId, request.UserId, cancellationToken))
            return new ForbiddenError("Only the tenant owner can configure SSO.");

        string authority;
        try
        {
            authority = TenantSsoSettings.NormalizeAuthority(request.OidcAuthority);
        }
        catch (ArgumentException ex)
        {
            return new ValidationError(nameof(request.OidcAuthority), ex.Message);
        }

        var tenantKey = TenantId.Create(request.TenantId);
        var existing = await _store.GetForUpdateAsync(tenantKey, cancellationToken);
        var now = _clock.Now;

        if (existing is null)
        {
            var created = TenantSsoSettings.Create(
                tenantKey,
                request.IsEnabled,
                authority,
                request.OidcClientId,
                request.OidcClientSecret,
                now);
            _store.Add(created);
            await _uow.SaveChangesAsync(cancellationToken);
            return TenantSsoSettingsDto.From(created);
        }

        existing.Update(request.IsEnabled, authority, request.OidcClientId, request.OidcClientSecret, now);
        await _uow.SaveChangesAsync(cancellationToken);
        return TenantSsoSettingsDto.From(existing);
    }
}

public sealed record TenantOidcSignInCommand(string TenantSlug, string IdToken, string? RemoteIpAddress = null)
    : IRequest<Result<AuthenticationResponse, Error>>;

public sealed class TenantOidcSignInCommandValidator : AbstractValidator<TenantOidcSignInCommand>
{
    public TenantOidcSignInCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty().MaximumLength(63);
        RuleFor(x => x.IdToken).NotEmpty().MaximumLength(8192);
    }
}

public sealed class
    TenantOidcSignInCommandHandler : IRequestHandler<TenantOidcSignInCommand, Result<AuthenticationResponse, Error>>
{
    private readonly IApplicationReadDbContext _db;
    private readonly IMediator _mediator;
    private readonly ITenantOidcIdTokenValidator _validator;

    public TenantOidcSignInCommandHandler(
        IApplicationReadDbContext db,
        ITenantOidcIdTokenValidator validator,
        IMediator mediator)
    {
        _db = db;
        _validator = validator;
        _mediator = mediator;
    }

    public async Task<Result<AuthenticationResponse, Error>> Handle(
        TenantOidcSignInCommand request,
        CancellationToken cancellationToken)
    {
        string slug;
        try
        {
            slug = Tenant.NormalizeSlug(request.TenantSlug);
        }
        catch (ArgumentException ex)
        {
            return new ValidationError(nameof(request.TenantSlug), ex.Message);
        }

        var tenantSso = await (
                from t in _db.Tenants.AsNoTracking()
                join s in _db.TenantSsoSettings.AsNoTracking() on t.Id equals s.TenantId
                where t.Slug == slug && t.Status == TenantStatus.Active && s.IsEnabled
                select new { TenantId = t.Id, s.OidcAuthority, s.OidcClientId })
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantSso is null)
            return new NotFoundError("Tenant SSO is not available.");

        var userInfo = await _validator.ValidateAsync(
            tenantSso.OidcAuthority,
            tenantSso.OidcClientId,
            request.IdToken,
            cancellationToken);
        if (userInfo is null)
            return new InvalidCredentialsError("OIDC token could not be validated.");

        return await _mediator.Send(
            new ProvisionOrSignInExternalUserCommand(
                tenantSso.TenantId.Value,
                userInfo.Subject,
                userInfo.Email,
                userInfo.GivenName,
                userInfo.FamilyName,
                request.RemoteIpAddress),
            cancellationToken);
    }
}