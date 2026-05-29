using Microsoft.AspNetCore.Mvc;

namespace Api.Filters;

/// <summary>Wraps the action in a write-side database transaction (see <see cref="TransactionalActionFilter" />).</summary>
public sealed class TransactionalAttribute : TypeFilterAttribute
{
    public TransactionalAttribute() : base(typeof(TransactionalActionFilter))
    {
    }
}