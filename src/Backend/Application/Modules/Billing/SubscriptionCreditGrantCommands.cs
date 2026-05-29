using Application.Contracts.Billing;
using Application.Contracts.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Application.Contracts.Wallet;
using Microsoft.EntityFrameworkCore;

namespace Application.Modules.Billing;

public sealed record ProcessSubscriptionCreditGrantsCommand : IRequest<Result<int, Error>>;

public sealed class ProcessSubscriptionCreditGrantsCommandHandler
    : IRequestHandler<ProcessSubscriptionCreditGrantsCommand, Result<int, Error>>
{
    private readonly IDateTimeProvider _clock;
    private readonly IApplicationReadDbContext _db;
    private readonly ISubscriptionStore _subscriptions;
    private readonly IUnitOfWork _uow;
    private readonly IWalletService _wallet;

    public ProcessSubscriptionCreditGrantsCommandHandler(
        ISubscriptionStore subscriptions,
        IApplicationReadDbContext db,
        IWalletService wallet,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _subscriptions = subscriptions;
        _db = db;
        _wallet = wallet;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<int, Error>> Handle(
        ProcessSubscriptionCreditGrantsCommand request,
        CancellationToken cancellationToken)
    {
        var now = _clock.Now;
        var grantPeriod = now.ToString("yyyy-MM");
        var granted = 0;

        var subscriptions =
            await _subscriptions.ListActiveOrTrialingForUpdateAsync(cancellationToken);

        foreach (var subscription in subscriptions)
        {
            if (!subscription.NeedsMonthlyCreditGrant(now))
                continue;

            var limitsJson = await _db.Plans.AsNoTracking()
                .Where(p => p.Id == subscription.PlanId)
                .Select(p => p.LimitsJson)
                .FirstOrDefaultAsync(cancellationToken);
            var limits = PlanLimits.Parse(limitsJson);
            if (limits.MonthlyCreditGrant <= 0)
                continue;

            var ownerUserId = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == subscription.TenantId)
                .Select(t => t.OwnerUserId.Value)
                .FirstOrDefaultAsync(cancellationToken);
            if (ownerUserId == Guid.Empty)
                continue;

            var idempotencyKey = $"plan-grant:{subscription.Id:N}:{grantPeriod}";
            await _wallet.TopUpAsync(
                ownerUserId,
                limits.MonthlyCreditGrant,
                idempotencyKey,
                $"plan-grant:{grantPeriod}",
                cancellationToken);

            subscription.RecordMonthlyCreditGrant(now);
            granted++;
        }

        if (granted > 0)
            await _uow.SaveChangesAsync(cancellationToken);

        return granted;
    }
}