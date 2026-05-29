namespace Application.Contracts.Authorization;

/// <summary>
///     Marker interface placed on a MediatR request to declare that it can only be executed by an
///     authenticated administrator. The <c>AdminAuthorizationBehavior</c> pipeline checks the
///     current principal's role claim before the handler runs, so command handlers no longer need
///     to re-query the database for the acting user.
/// </summary>
public interface IRequireAdmin
{
}