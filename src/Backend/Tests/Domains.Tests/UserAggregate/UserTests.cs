using Domains.UserAggregate;

namespace Domains.Tests.UserAggregate;

/// <summary>
/// Unit tests for the <see cref="User"/> aggregate. These cover the factory contract (rejecting
/// invalid combinations), state-machine guards on lockout/2FA, and idempotency of one-shot
/// transitions like deletion or email verification.
/// </summary>
public sealed class UserTests
{
    private const string FirstName = "Test";
    private const string LastName = "User";
    private const string UserName = "tuser";
    private const string Email = "test@example.com";
    private const string ValidHash = "AQAAAAEAACcQAAAAEM_hash_payload";

    [Fact]
    public void CreateNew_throws_when_password_hash_is_empty()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => User.CreateNew(FirstName, LastName, UserName, Email, passwordHash: string.Empty));

        Assert.Equal("passwordHash", ex.ParamName);
    }

    [Fact]
    public void CreateNew_produces_user_with_default_role()
    {
        User user = User.CreateNew(FirstName, LastName, UserName, Email, ValidHash);

        Assert.Equal(UserRoles.User, user.Role);
        Assert.Equal(ValidHash, user.PasswordHash);
        Assert.False(user.IsLocked);
        Assert.Null(user.DeletedAtUtc);
        Assert.Null(user.EmailVerifiedAtUtc);
        Assert.False(user.TwoFactorEnabled);
        Assert.True(user.CanSignIn);
    }

    [Fact]
    public void CreateForLocalLoginPendingHash_leaves_password_empty_until_initial_hash_is_set()
    {
        User user = User.CreateForLocalLoginPendingHash(FirstName, LastName, UserName, Email);
        Assert.Equal(string.Empty, user.PasswordHash);

        user.SetInitialPasswordHash(ValidHash);
        Assert.Equal(ValidHash, user.PasswordHash);
    }

    [Fact]
    public void SetInitialPasswordHash_throws_when_already_set()
    {
        User user = User.CreateForLocalLoginPendingHash(FirstName, LastName, UserName, Email);
        user.SetInitialPasswordHash(ValidHash);

        Assert.Throws<InvalidOperationException>(() => user.SetInitialPasswordHash("AQAAAAEAACcQAAAAEM_other"));
    }

    [Fact]
    public void SetInitialPasswordHash_rejects_empty_payload()
    {
        User user = User.CreateForLocalLoginPendingHash(FirstName, LastName, UserName, Email);

        Assert.Throws<ArgumentException>(() => user.SetInitialPasswordHash(string.Empty));
    }

    [Fact]
    public void UpdatePasswordHash_rejects_empty_payload_to_prevent_stale_hash_overwrites()
    {
        User user = User.CreateNew(FirstName, LastName, UserName, Email, ValidHash);

        Assert.Throws<ArgumentException>(() => user.UpdatePasswordHash(string.Empty));
        Assert.Equal(ValidHash, user.PasswordHash);
    }

    [Fact]
    public void Lock_sets_flag_and_reason()
    {
        User user = User.CreateNew(FirstName, LastName, UserName, Email, ValidHash);

        user.Lock("policy");

        Assert.True(user.IsLocked);
        Assert.Equal("policy", user.LockReason);
        Assert.False(user.CanSignIn);
    }

    [Fact]
    public void Lock_throws_when_user_is_deleted()
    {
        User user = User.CreateNew(FirstName, LastName, UserName, Email, ValidHash);
        user.MarkDeleted();

        Assert.Throws<InvalidOperationException>(() => user.Lock("nope"));
    }

    [Fact]
    public void Unlock_is_idempotent_and_clears_reason()
    {
        User user = User.CreateNew(FirstName, LastName, UserName, Email, ValidHash);

        user.Unlock(); // first call on already-unlocked user: no-op
        user.Lock("temporary");
        user.Unlock();

        Assert.False(user.IsLocked);
        Assert.Null(user.LockReason);
    }

    [Fact]
    public void MarkDeleted_is_idempotent_and_keeps_first_timestamp()
    {
        User user = User.CreateNew(FirstName, LastName, UserName, Email, ValidHash);

        user.MarkDeleted();
        DateTime first = user.DeletedAtUtc!.Value;
        Thread.Sleep(5);
        user.MarkDeleted();

        Assert.Equal(first, user.DeletedAtUtc);
    }

    [Fact]
    public void MarkEmailVerified_keeps_original_timestamp_when_called_again()
    {
        User user = User.CreateNew(FirstName, LastName, UserName, Email, ValidHash);
        DateTime original = new(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        user.MarkEmailVerified(original);
        user.MarkEmailVerified(original.AddDays(1));

        Assert.Equal(original, user.EmailVerifiedAtUtc);
    }

    [Fact]
    public void MarkRegisteredAt_keeps_first_value()
    {
        User user = User.CreateNew(FirstName, LastName, UserName, Email, ValidHash);
        DateTime first = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        user.MarkRegisteredAt(first);
        user.MarkRegisteredAt(first.AddYears(1));

        Assert.Equal(first, user.RegisteredAtUtc);
    }

    [Fact]
    public void SetTwoFactorPendingSecret_disables_2fa_until_activate_is_called()
    {
        User user = User.CreateNew(FirstName, LastName, UserName, Email, ValidHash);

        user.SetTwoFactorPendingSecret("JBSWY3DPEHPK3PXP");

        Assert.Equal("JBSWY3DPEHPK3PXP", user.TwoFactorSecret);
        Assert.False(user.TwoFactorEnabled);
    }

    [Fact]
    public void ActivateTwoFactor_throws_when_pending_secret_was_never_set()
    {
        User user = User.CreateNew(FirstName, LastName, UserName, Email, ValidHash);

        Assert.Throws<InvalidOperationException>(() => user.ActivateTwoFactor());
    }

    [Fact]
    public void ActivateTwoFactor_sets_enabled_flag_when_secret_present()
    {
        User user = User.CreateNew(FirstName, LastName, UserName, Email, ValidHash);
        user.SetTwoFactorPendingSecret("JBSWY3DPEHPK3PXP");

        user.ActivateTwoFactor();

        Assert.True(user.TwoFactorEnabled);
    }

    [Fact]
    public void DisableTwoFactor_clears_state_completely()
    {
        User user = User.CreateNew(FirstName, LastName, UserName, Email, ValidHash);
        user.SetTwoFactorPendingSecret("JBSWY3DPEHPK3PXP");
        user.ActivateTwoFactor();

        user.DisableTwoFactor();

        Assert.False(user.TwoFactorEnabled);
        Assert.Null(user.TwoFactorSecret);
    }

    [Theory]
    [InlineData(true, false, false, false)]   // deleted -> cannot sign in
    [InlineData(false, true, false, false)]   // closed  -> cannot sign in
    [InlineData(false, false, true, false)]   // locked  -> cannot sign in
    [InlineData(false, false, false, true)]   // healthy
    public void CanSignIn_reflects_lifecycle_flags(bool deleted, bool closed, bool locked, bool expected)
    {
        User user = User.CreateNew(FirstName, LastName, UserName, Email, ValidHash);

        if (deleted) user.MarkDeleted();
        if (closed) user.CloseAccount();
        if (locked && !deleted) user.Lock("test");

        Assert.Equal(expected, user.CanSignIn);
    }

    [Fact]
    public void AssignRole_changes_role_and_persists()
    {
        User user = User.CreateNew(FirstName, LastName, UserName, Email, ValidHash);

        user.AssignRole(UserRoles.Admin);

        Assert.Equal(UserRoles.Admin, user.Role);
    }

    [Fact]
    public void CreateForExternal_starts_with_empty_password_hash()
    {
        User user = User.CreateForExternal(FirstName, LastName, UserName, Email);

        Assert.Equal(string.Empty, user.PasswordHash);
        Assert.True(user.CanSignIn);
    }
}
