using Application.Contracts.Transactions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Api.Filters;

/// <summary>
/// Begins an EF Core database transaction before the action and commits or rolls back afterward.
/// Pair with <see cref="TransactionalAttribute"/> on controllers or actions that call command handlers using the scoped write DbContext.
/// </summary>
public sealed class TransactionalActionFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _db;

    public TransactionalActionFilter(IUnitOfWork unitOfWork, ApplicationDbContext db)
    {
        _unitOfWork = unitOfWork;
        _db = db;
    }

    public int Order => -1000;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        CancellationToken ct = context.HttpContext.RequestAborted;

        if (!_db.Database.IsRelational())
        {
            await ExecuteOnceAsync(next, ct);
            return;
        }

        // NpgsqlRetryingExecutionStrategy requires any explicit transaction to be created inside
        // CreateExecutionStrategy().ExecuteAsync, otherwise even a simple query inside the action
        // throws "does not support user-initiated transactions".
        IExecutionStrategy strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async innerCt => await ExecuteOnceAsync(next, innerCt), ct);
    }

    private async Task ExecuteOnceAsync(ActionExecutionDelegate next, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        ActionExecutedContext executed;
        try
        {
            executed = await next();
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return;
        }

        int statusCode = GetStatusCode(executed.Result);
        if (statusCode >= 400)
            await _unitOfWork.RollbackAsync(cancellationToken);
        else
            await _unitOfWork.CommitAsync(cancellationToken);
    }

    private static int GetStatusCode(IActionResult? result)
    {
        return result switch
        {
            IStatusCodeActionResult s when s.StatusCode.HasValue => s.StatusCode.Value,
            ObjectResult { StatusCode: int sc } => sc,
            _ => 200
        };
    }
}
