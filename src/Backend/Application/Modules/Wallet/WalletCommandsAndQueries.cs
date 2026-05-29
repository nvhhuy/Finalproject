using Application.Contracts.Wallet;

namespace Application.Modules.UserWallet;

public sealed record GetWalletBalanceQuery(Guid UserId) : IRequest<Result<WalletBalanceDto, Error>>;

public sealed class
    GetWalletBalanceQueryHandler : IRequestHandler<GetWalletBalanceQuery, Result<WalletBalanceDto, Error>>
{
    private readonly IWalletService _wallet;

    public GetWalletBalanceQueryHandler(IWalletService wallet)
    {
        _wallet = wallet;
    }

    public async Task<Result<WalletBalanceDto, Error>> Handle(GetWalletBalanceQuery request,
        CancellationToken cancellationToken)
    {
        return await _wallet.GetBalanceAsync(request.UserId, cancellationToken);
    }
}

public sealed record GetWalletLedgerQuery(Guid UserId, int Page = 1, int PageSize = 20)
    : IRequest<Result<IReadOnlyList<WalletLedgerEntryDto>, Error>>;

public sealed class
    GetWalletLedgerQueryHandler : IRequestHandler<GetWalletLedgerQuery,
    Result<IReadOnlyList<WalletLedgerEntryDto>, Error>>
{
    private readonly IWalletService _wallet;

    public GetWalletLedgerQueryHandler(IWalletService wallet)
    {
        _wallet = wallet;
    }

    public async Task<Result<IReadOnlyList<WalletLedgerEntryDto>, Error>> Handle(
        GetWalletLedgerQuery request,
        CancellationToken cancellationToken)
    {
        var rows =
            (await _wallet.GetLedgerAsync(request.UserId, request.Page, request.PageSize, cancellationToken)).ToList();
        return rows;
    }
}

public sealed record TopUpWalletCommand(Guid UserId, long AmountCredits, string IdempotencyKey)
    : IRequest<Result<WalletBalanceDto, Error>>;

public sealed class TopUpWalletCommandValidator : AbstractValidator<TopUpWalletCommand>
{
    public TopUpWalletCommandValidator()
    {
        RuleFor(x => x.AmountCredits).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

public sealed class TopUpWalletCommandHandler : IRequestHandler<TopUpWalletCommand, Result<WalletBalanceDto, Error>>
{
    private readonly IWalletService _wallet;

    public TopUpWalletCommandHandler(IWalletService wallet)
    {
        _wallet = wallet;
    }

    public async Task<Result<WalletBalanceDto, Error>> Handle(TopUpWalletCommand request,
        CancellationToken cancellationToken)
    {
        return await _wallet.TopUpAsync(
            request.UserId,
            request.AmountCredits,
            request.IdempotencyKey,
            cancellationToken: cancellationToken);
    }
}

public sealed record DebitWalletActionCommand(Guid UserId, string ActionCode, string IdempotencyKey)
    : IRequest<Result<WalletDebitResultDto, Error>>;

public sealed class DebitWalletActionCommandValidator : AbstractValidator<DebitWalletActionCommand>
{
    public DebitWalletActionCommandValidator()
    {
        RuleFor(x => x.ActionCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

public sealed class
    DebitWalletActionCommandHandler : IRequestHandler<DebitWalletActionCommand, Result<WalletDebitResultDto, Error>>
{
    private readonly IWalletService _wallet;

    public DebitWalletActionCommandHandler(IWalletService wallet)
    {
        _wallet = wallet;
    }

    public async Task<Result<WalletDebitResultDto, Error>> Handle(
        DebitWalletActionCommand request,
        CancellationToken cancellationToken)
    {
        return await _wallet.DebitForActionAsync(request.UserId, request.ActionCode, request.IdempotencyKey,
            cancellationToken);
    }
}

public sealed record ListMeteredActionsQuery : IRequest<Result<IReadOnlyList<MeteredActionDto>, Error>>;

public sealed class
    ListMeteredActionsQueryHandler : IRequestHandler<ListMeteredActionsQuery,
    Result<IReadOnlyList<MeteredActionDto>, Error>>
{
    public Task<Result<IReadOnlyList<MeteredActionDto>, Error>> Handle(
        ListMeteredActionsQuery request,
        CancellationToken cancellationToken)
    {
        var catalog = MeteredActions.ListCatalog().ToList();
        return Task.FromResult<Result<IReadOnlyList<MeteredActionDto>, Error>>(catalog);
    }
}