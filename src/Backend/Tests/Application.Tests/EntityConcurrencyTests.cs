using Application.Common;
using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Tests;

public sealed class EntityConcurrencyTests
{
    [Fact]
    public void ValidateExpectedVersion_returns_null_when_versions_match()
    {
        User user = User.Create(UserId.Create(Guid.NewGuid()), "A", "B", "u", "u@e.com", "hash");

        Assert.Null(EntityConcurrency.ValidateExpectedVersion(user, user.RowVersion));
    }

    [Fact]
    public void ValidateExpectedVersion_returns_conflict_when_versions_differ()
    {
        User user = User.Create(UserId.Create(Guid.NewGuid()), "A", "B", "u", "u@e.com", "hash");

        Error? error = EntityConcurrency.ValidateExpectedVersion(user, user.RowVersion + 1);

        Assert.IsType<ConcurrencyConflictError>(error);
    }
}
