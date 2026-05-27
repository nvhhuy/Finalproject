using Domains.Common;
using Domains.UserAggregate.ValueObjects;

namespace Domains.UserAggregate
{
    public class User : AggregateRoot<UserId>, IHasRowVersion
    {
        public string UserName { get; private set; } = null!;
        public string FirstName { get; private set; } = null!;
        public string LastName { get; private set; } = null!;
        public string Email { get; private set; } = null!;

        /// <summary>
        /// Upper-invariant copy of <see cref="UserName"/> for case-insensitive uniqueness and
        /// lookups (ASP.NET Identity convention). Set only by the aggregate when the underlying
        /// value changes; EF and read-repos always compare against this column so the existing
        /// unique index can be used without a lower(...) functional wrapper.
        /// </summary>
        public string NormalizedUserName { get; private set; } = null!;

        /// <summary>Upper-invariant copy of <see cref="Email"/>; see <see cref="NormalizedUserName"/>.</summary>
        public string NormalizedEmail { get; private set; } = null!;
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

        /// <summary>Optimistic concurrency token; incremented on every successful update.</summary>
        public uint RowVersion { get; private set; }

        uint IHasRowVersion.RowVersion => RowVersion;

        public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();
        public ICollection<ExternalUserLogin> ExternalLogins { get; private set; } = new List<ExternalUserLogin>();

        private User() { } // for ef core

        private User(UserId id, string firstName, string lastName, string userName, string email, string passwordHash) : base(id)
        {
            FirstName = firstName;
            LastName = lastName;
            UserName = userName;
            Email = email;
            NormalizedUserName = Normalize(userName);
            NormalizedEmail = Normalize(email);
            PasswordHash = passwordHash;
        }

        /// <summary>
        /// Canonical case-folding for lookup columns. Identity uses Upper-Invariant — we keep the
        /// same so anyone moving between this codebase and a vanilla Identity stack doesn't get
        /// silent mismatches. Whitespace is trimmed so accidental leading/trailing spaces don't
        /// produce phantom non-unique values.
        /// </summary>
        private static string Normalize(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();

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

        /// <summary>
        /// Updates the editable profile fields. Each field is required (rejects null/whitespace) so
        /// invalid input is rejected at the aggregate boundary rather than relying solely on
        /// FluentValidation in the application layer.
        /// </summary>
        public void UpdateProfile(string firstName, string lastName, string userName, string email)
        {
            if (string.IsNullOrWhiteSpace(firstName))
                throw new ArgumentException("First name cannot be empty.", nameof(firstName));
            if (string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("Last name cannot be empty.", nameof(lastName));
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentException("User name cannot be empty.", nameof(userName));
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty.", nameof(email));

            FirstName = firstName;
            LastName = lastName;
            UserName = userName;
            Email = email;
            NormalizedUserName = Normalize(userName);
            NormalizedEmail = Normalize(email);
        }

        /// <summary>
        /// Sets the role claim. Only values defined in <see cref="UserRoles"/> are allowed; passing
        /// an unknown string throws so we never persist a typo that would silently disable
        /// authorization checks.
        /// </summary>
        public void AssignRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                throw new ArgumentException("Role cannot be empty.", nameof(role));
            if (role != UserRoles.User && role != UserRoles.Admin)
                throw new ArgumentException($"Unknown role '{role}'. Expected '{UserRoles.User}' or '{UserRoles.Admin}'.", nameof(role));
            Role = role;
        }

        /// <summary>
        /// Locks the account, optionally recording a reason. Cannot lock a deleted or closed
        /// account — those states are terminal for the user lifecycle and locking would be a
        /// no-op that hides bugs in the caller.
        /// </summary>
        public void Lock(string? reason = null)
        {
            if (DeletedAtUtc is not null)
                throw new InvalidOperationException("Cannot lock a deleted user.");
            if (AccountClosedAtUtc is not null)
                throw new InvalidOperationException("Cannot lock a closed account.");
            IsLocked = true;
            LockReason = reason;
        }

        /// <summary>Releases the lock. Idempotent.</summary>
        public void Unlock()
        {
            IsLocked = false;
            LockReason = null;
        }

        /// <summary>
        /// Marks the user as deleted at <paramref name="utcNow"/>. Idempotent — the first call
        /// wins so a duplicate command from a retry doesn't overwrite the forensic timestamp.
        /// The parameter exists so tests can pass a controlled clock and verify idempotency
        /// without resorting to <c>Thread.Sleep</c> on a real wall clock.
        /// </summary>
        public void MarkDeleted(DateTime utcNow)
        {
            if (DeletedAtUtc is not null)
                return;
            DeletedAtUtc = utcNow;
        }

        public void MarkDeleted() => MarkDeleted(DateTime.UtcNow);

        public void CloseAccount(DateTime utcNow)
        {
            if (AccountClosedAtUtc is not null)
                return;
            AccountClosedAtUtc = utcNow;
        }

        public void CloseAccount() => CloseAccount(DateTime.UtcNow);

        /// <summary>Marks the user as having signed in at <paramref name="utcNow"/>.</summary>
        /// <remarks>
        /// Takes the clock as a parameter so callers can pass <c>IDateTimeProvider.Now</c> and tests
        /// can use a fixed instant. Older code calling <c>RecordLogin()</c> with no args is preserved
        /// via the parameterless overload.
        /// </remarks>
        public void RecordLogin(DateTime utcNow) => LastLoginAtUtc = utcNow;

        public void RecordLogin() => RecordLogin(DateTime.UtcNow);

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
