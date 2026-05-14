using Domains.Common;
using Domains.UserAggregate.ValueObjects;

namespace Domains.UserAggregate
{
    public class User : AggregateRoot<UserId>
    {
        public string UserName { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;

        public string Role { get; private set; } = UserRoles.User;
        public bool IsLocked { get; private set; }
        public string? LockReason { get; private set; }
        public DateTime? DeletedAtUtc { get; private set; }
        public DateTime? AccountClosedAtUtc { get; private set; }
        public bool TwoFactorEnabled { get; private set; }
        /// <summary>Base32 TOTP secret (protect at rest in production, e.g. field-level encryption).</summary>
        public string? TwoFactorSecret { get; private set; }
        public DateTime? LastLoginAtUtc { get; private set; }
        /// <summary>When the email address was confirmed (self-service link or admin).</summary>
        public DateTime? EmailVerifiedAtUtc { get; private set; }
        /// <summary>Account creation time (used for admin metrics).</summary>
        public DateTime? RegisteredAtUtc { get; private set; }

        public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();
        public ICollection<ExternalUserLogin> ExternalLogins { get; private set; } = new List<ExternalUserLogin>();

        private User() { } // for ef core

        private User(UserId id, string firstName, string lastName, string userName, string email, string password) : base(id)
        {
            FirstName = firstName;
            LastName = lastName;
            UserName = userName;
            Email = email;
            Password = password;
        }

        public static User Create(UserId id, string firstName, string lastName, string userName, string email, string password) =>
            new(id, firstName, lastName, userName, email, password);

        public static User CreateNew(string firstName, string lastName, string userName, string email, string password) =>
            new(UserId.CreateNew(), firstName, lastName, userName, email, password);

        public static User CreateForExternal(string firstName, string lastName, string userName, string email) =>
            new(UserId.CreateNew(), firstName, lastName, userName, email, password: string.Empty);

        public void UpdatePassword(string password) => Password = password;

        public void UpdateProfile(string firstName, string lastName, string userName, string email)
        {
            FirstName = firstName;
            LastName = lastName;
            UserName = userName;
            Email = email;
        }

        public void AssignRole(string role) => Role = role;

        public void Lock(string? reason = null)
        {
            IsLocked = true;
            LockReason = reason;
        }

        public void Unlock()
        {
            IsLocked = false;
            LockReason = null;
        }

        public void MarkDeleted() => DeletedAtUtc = DateTime.UtcNow;

        public void CloseAccount() => AccountClosedAtUtc = DateTime.UtcNow;

        public void RecordLogin() => LastLoginAtUtc = DateTime.UtcNow;

        public void MarkRegisteredAt(DateTime utc) => RegisteredAtUtc = utc;

        public void MarkEmailVerified(DateTime verifiedAtUtc) => EmailVerifiedAtUtc = verifiedAtUtc;

        public void SetTwoFactorPendingSecret(string base32Secret)
        {
            TwoFactorSecret = base32Secret;
            TwoFactorEnabled = false;
        }

        public void ActivateTwoFactor()
        {
            if (string.IsNullOrWhiteSpace(TwoFactorSecret))
                throw new InvalidOperationException("Two-factor secret is not set.");
            TwoFactorEnabled = true;
        }

        public void DisableTwoFactor()
        {
            TwoFactorEnabled = false;
            TwoFactorSecret = null;
        }

        public bool CanSignIn => DeletedAtUtc is null && AccountClosedAtUtc is null && !IsLocked;
    }
}
