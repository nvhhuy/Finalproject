using Application.Contracts.Authorization;
using Application.Contracts.Billing;
using Application.Contracts.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Application.Contracts.Wallet;
using Domains.Billing;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Billing;

public sealed record CreditPackageDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int PriceCents,
    string Currency,
    long CreditsAmount,
    bool IsActive,
    int SortOrder);

public sealed record ListCreditPackagesQuery(bool ActiveOnly = true)
    : IRequest<Result<IReadOnlyList<CreditPackageDto>, Error>>;

public sealed class ListCreditPackagesQueryHandler
    : IRequestHandler<ListCreditPackagesQuery, Result<IReadOnlyList<CreditPackageDto>, Error>>
{
    private readonly IApplicationReadDbContext _db;

    public ListCreditPackagesQueryHandler(IApplicationReadDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<CreditPackageDto>, Error>> Handle(
        ListCreditPackagesQuery request,
        CancellationToken cancellationToken)
    {
        IQueryable<CreditPackage> q = _db.CreditPackages.AsNoTracking();
        if (request.ActiveOnly)
            q = q.Where(p => p.IsActive);

        List<CreditPackageDto> rows = await q
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .Select(p => new CreditPackageDto(
                p.Id,
                p.Code,
                p.Name,
                p.Description,
                p.PriceCents,
                p.Currency,
                p.CreditsAmount,
                p.IsActive,
                p.SortOrder))
            .ToListAsync(cancellationToken);

        return rows;
    }
}

public sealed record CreateCreditPackageCommand(
    string Code,
    string Name,
    string? Description,
    int PriceCents,
    string Currency,
    long CreditsAmount,
    int SortOrder) : IRequest<Result<CreditPackageDto, Error>>, IRequireAdmin;

public sealed class CreateCreditPackageCommandValidator : AbstractValidator<CreateCreditPackageCommand>
{
    public CreateCreditPackageCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PriceCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CreditsAmount).GreaterThan(0);
    }
}

public sealed class CreateCreditPackageCommandHandler
    : IRequestHandler<CreateCreditPackageCommand, Result<CreditPackageDto, Error>>
{
    private readonly ICreditPackageStore _packages;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public CreateCreditPackageCommandHandler(ICreditPackageStore packages, IUnitOfWork uow, IDateTimeProvider clock)
    {
        _packages = packages;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<CreditPackageDto, Error>> Handle(
        CreateCreditPackageCommand request,
        CancellationToken cancellationToken)
    {
        string code = CreditPackage.NormalizeCode(request.Code);
        if (await _packages.CodeExistsAsync(code, cancellationToken))
            return new ValidationError(nameof(request.Code), "Credit package code already exists.");

        CreditPackage package = CreditPackage.Create(
            code,
            request.Name,
            request.Description,
            request.PriceCents,
            request.Currency,
            request.CreditsAmount,
            request.SortOrder,
            _clock.Now);
        _packages.Add(package);
        await _uow.SaveChangesAsync(cancellationToken);
        return ToDto(package);
    }

    internal static CreditPackageDto ToDto(CreditPackage package) =>
        new(
            package.Id,
            package.Code,
            package.Name,
            package.Description,
            package.PriceCents,
            package.Currency,
            package.CreditsAmount,
            package.IsActive,
            package.SortOrder);
}

public sealed record UpdateCreditPackageCommand(
    Guid PackageId,
    string Name,
    string? Description,
    int PriceCents,
    string Currency,
    long CreditsAmount,
    bool IsActive,
    int SortOrder) : IRequest<Result<CreditPackageDto, Error>>, IRequireAdmin;

public sealed class UpdateCreditPackageCommandValidator : AbstractValidator<UpdateCreditPackageCommand>
{
    public UpdateCreditPackageCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PriceCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CreditsAmount).GreaterThan(0);
    }
}

public sealed class UpdateCreditPackageCommandHandler
    : IRequestHandler<UpdateCreditPackageCommand, Result<CreditPackageDto, Error>>
{
    private readonly ICreditPackageStore _packages;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public UpdateCreditPackageCommandHandler(ICreditPackageStore packages, IUnitOfWork uow, IDateTimeProvider clock)
    {
        _packages = packages;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<CreditPackageDto, Error>> Handle(
        UpdateCreditPackageCommand request,
        CancellationToken cancellationToken)
    {
        CreditPackage? package = await _packages.GetByIdForUpdateAsync(request.PackageId, cancellationToken);
        if (package is null)
            return new NotFoundError("Credit package was not found.");

        package.Update(
            request.Name,
            request.Description,
            request.PriceCents,
            request.Currency,
            request.CreditsAmount,
            request.IsActive,
            request.SortOrder,
            _clock.Now);
        await _uow.SaveChangesAsync(cancellationToken);
        return ToDto(package);
    }

    private static CreditPackageDto ToDto(CreditPackage package) => CreateCreditPackageCommandHandler.ToDto(package);
}

public sealed record DeleteCreditPackageCommand(Guid PackageId)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class DeleteCreditPackageCommandHandler
    : IRequestHandler<DeleteCreditPackageCommand, Result<bool, Error>>
{
    private readonly ICreditPackageStore _packages;
    private readonly IUnitOfWork _uow;

    public DeleteCreditPackageCommandHandler(ICreditPackageStore packages, IUnitOfWork uow)
    {
        _packages = packages;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(DeleteCreditPackageCommand request, CancellationToken cancellationToken)
    {
        CreditPackage? package = await _packages.GetByIdForUpdateAsync(request.PackageId, cancellationToken);
        if (package is null)
            return new NotFoundError("Credit package was not found.");

        _packages.Remove(package);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}

/// <summary>
/// Fulfills a credit package purchase (MVP: immediate wallet credit; production should gate on payment webhook).
/// </summary>
public sealed record PurchaseCreditPackageCommand(Guid UserId, string PackageCode, string IdempotencyKey)
    : IRequest<Result<WalletBalanceDto, Error>>;

public sealed class PurchaseCreditPackageCommandValidator : AbstractValidator<PurchaseCreditPackageCommand>
{
    public PurchaseCreditPackageCommandValidator()
    {
        RuleFor(x => x.PackageCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

public sealed class PurchaseCreditPackageCommandHandler
    : IRequestHandler<PurchaseCreditPackageCommand, Result<WalletBalanceDto, Error>>
{
    private readonly ICreditPackageStore _packages;
    private readonly IWalletService _wallet;

    public PurchaseCreditPackageCommandHandler(ICreditPackageStore packages, IWalletService wallet)
    {
        _packages = packages;
        _wallet = wallet;
    }

    public async Task<Result<WalletBalanceDto, Error>> Handle(
        PurchaseCreditPackageCommand request,
        CancellationToken cancellationToken)
    {
        string code = CreditPackage.NormalizeCode(request.PackageCode);
        CreditPackage? package = await _packages.GetByCodeAsync(code, activeOnly: true, cancellationToken);
        if (package is null)
            return new NotFoundError("Credit package was not found or is inactive.");

        return await _wallet.PurchasePackageAsync(
            request.UserId,
            package.Id,
            package.Code,
            package.CreditsAmount,
            package.PriceCents,
            package.Currency,
            request.IdempotencyKey,
            provider: "manual",
            cancellationToken);
    }
}
