using Application.Common;
using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Shared.Errors.General;

namespace Application.Tests;

public sealed class EntityConcurrencyTests
{
    [Fact]
    public void ValidateExpectedVersion_returns_null_when_versions_match()
    {
        var user = User.Create(UserId.Create(Guid.NewGuid()), "A", "B", "u", "u@e.com", "hash");

        Assert.Null(EntityConcurrency.ValidateExpectedVersion(user, user.RowVersion));
    }

    [Fact]
    public void ValidateExpectedVersion_returns_conflict_when_versions_differ()
    {
        var user = User.Create(UserId.Create(Guid.NewGuid()), "A", "B", "u", "u@e.com", "hash");

        var error = EntityConcurrency.ValidateExpectedVersion(user, user.RowVersion + 1);

        Assert.IsType<ConcurrencyConflictError>(error);
    }
}