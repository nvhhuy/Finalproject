using Application.Contracts.Transactions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Api.Filters;

/// <summary>
/// Begins an EF Core database transaction before the action and commits or rolls back afterward.
/// Pair with <see cref="TransactionalAttribute"/> on controllers or actions that call command handlers using the scoped write DbContext.
/// </summary>
public sealed class TransactionalActionFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly IUnitOfWork _unitOfWork;

    public TransactionalActionFilter(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public int Order => -1000;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await _unitOfWork.BeginTransactionAsync(context.HttpContext.RequestAborted);

        ActionExecutedContext executed;
        try
        {
            executed = await next();
        }
        catch
        {
            await _unitOfWork.RollbackAsync(context.HttpContext.RequestAborted);
            throw;
        }

        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            await _unitOfWork.RollbackAsync(context.HttpContext.RequestAborted);
            return;
        }

        int statusCode = GetStatusCode(executed.Result);
        if (statusCode >= 400)
            await _unitOfWork.RollbackAsync(context.HttpContext.RequestAborted);
        else
            await _unitOfWork.CommitAsync(context.HttpContext.RequestAborted);
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
