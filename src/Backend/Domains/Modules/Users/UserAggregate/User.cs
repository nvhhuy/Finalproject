using Domains.Common;
using Domains.UserAggregate.ValueObjects;

namespace Domains.UserAggregate
{
    public class User : AggregateRoot<UserId>
    {
        public string UserName { get; private set; } = null!;
        public string FirstName { get; private set; } = null!;
        public string LastName { get; private set; } = null!;
        public string Email { get; private set; } = null!;
        /// <summary>Hashed password (PBKDF2 via ASP.NET Identity hasher). Never plaintext.</summary>
        public string PasswordHash { get; private set; } = null!;

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

        private User(UserId id, string firstName, string lastName, string userName, string email, string passwordHash) : base(id)
        {
            FirstName = firstName;
            LastName = lastName;
            UserName = userName;
            Email = email;
            PasswordHash = passwordHash;
        }

        /// <summary>Rehydrates a User from persistence. Used by seeders / migrations only.</summary>
        public static User Create(UserId id, string firstName, string lastName, string userName, string email, string passwordHash) =>
            new(id, firstName, lastName, userName, email, passwordHash);

        /// <summary>
        /// Factory for self-service or admin-created local accounts where the caller has already
        /// computed a non-empty password hash. Use <see cref="CreateForLocalLoginPendingHash"/>
        /// when you need a User instance to feed into <c>IPasswordHasher.HashPassword</c> first.
        /// </summary>
        public static User CreateNew(string firstName, string lastName, string userName, string email, string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException(
                    "Password hash cannot be empty. Use CreateForLocalLoginPendingHash + SetInitialPasswordHash if you need to hash plaintext after construction.",
                    nameof(passwordHash));
            return new(UserId.CreateNew(), firstName, lastName, userName, email, passwordHash);
        }

        /// <summary>
        /// Two-step local-login factory: returns a User with an empty <see cref="PasswordHash"/>
        /// so the caller can compute the hash (which requires the User instance for the ASP.NET
        /// Identity API) and then call <see cref="SetInitialPasswordHash"/> exactly once.
        /// </summary>
        public static User CreateForLocalLoginPendingHash(string firstName, string lastName, string userName, string email) =>
            new(UserId.CreateNew(), firstName, lastName, userName, email, passwordHash: string.Empty);

        /// <summary>External-login factory (Google / OAuth) — no local password.</summary>
        public static User CreateForExternal(string firstName, string lastName, string userName, string email) =>
            new(UserId.CreateNew(), firstName, lastName, userName, email, passwordHash: string.Empty);

        /// <summary>Sets the initial password hash on a freshly-constructed User. Throws if already set.</summary>
        public void SetInitialPasswordHash(string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));
            if (!string.IsNullOrEmpty(PasswordHash))
                throw new InvalidOperationException("Initial password hash is already set; use UpdatePasswordHash for subsequent changes.");
            PasswordHash = passwordHash;
        }

        public void UpdatePasswordHash(string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));
            PasswordHash = passwordHash;
        }

        public void UpdateProfile(string firstName, string lastName, string userName, string email)
        {
            FirstName = firstName;
            LastName = lastName;
            UserName = userName;
            Email = email;
        }

        public void AssignRole(string role) => Role = role;

        /// <summary>Locks the account, optionally recording a reason. Cannot lock a deleted account.</summary>
        public void Lock(string? reason = null)
        {
            if (DeletedAtUtc is not null)
                throw new InvalidOperationException("Cannot lock a deleted user.");
            IsLocked = true;
            LockReason = reason;
        }

        /// <summary>Releases the lock. Idempotent.</summary>
        public void Unlock()
        {
            IsLocked = false;
            LockReason = null;
        }

        public void MarkDeleted()
        {
            if (DeletedAtUtc is not null)
                return; // idempotent
            DeletedAtUtc = DateTime.UtcNow;
        }

        public void CloseAccount()
        {
            if (AccountClosedAtUtc is not null)
                return;
            AccountClosedAtUtc = DateTime.UtcNow;
        }

        public void RecordLogin() => LastLoginAtUtc = DateTime.UtcNow;

        public void MarkRegisteredAt(DateTime utc)
        {
            if (RegisteredAtUtc is not null)
                return; // first registration timestamp wins
            RegisteredAtUtc = utc;
        }

        public void MarkEmailVerified(DateTime verifiedAtUtc)
        {
            if (EmailVerifiedAtUtc is not null)
                return; // already verified — idempotent
            EmailVerifiedAtUtc = verifiedAtUtc;
        }

        public void SetTwoFactorPendingSecret(string base32Secret)
        {
            if (string.IsNullOrWhiteSpace(base32Secret))
                throw new ArgumentException("Two-factor secret cannot be empty.", nameof(base32Secret));
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
