using Domains.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Common;

/// <summary>
/// Client-supplied <see cref="IHasRowVersion.RowVersion"/> checks before applying mutations.
/// EF still increments the token on <c>SaveChanges</c> as a second line of defense.
/// </summary>
public static class EntityConcurrency
{
    public static Error? ValidateExpectedVersion(IHasRowVersion entity, uint expectedRowVersion) =>
        entity.RowVersion != expectedRowVersion ? new ConcurrencyConflictError() : null;
}
