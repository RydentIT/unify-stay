using Unify.Domain.Common;

namespace Unify.Domain.Users;

/// <summary>
/// A registered account.
///
/// The methods here carry genuine business rules - the transitions an account may and may not
/// make. Shape checks (is this a well-formed email, is this password long enough) deliberately
/// live in the Application layer's FluentValidation validators instead, so this type only
/// refuses things that would leave the account in an incoherent state.
/// </summary>
public sealed class User : Entity
{
    private readonly List<RoleName> _roles = [];
    private readonly List<AuthProvider> _authProviders = [];

    public User(
        Guid id,
        string firstName,
        string lastName,
        Email email,
        string? passwordHash,
        bool emailVerified,
        bool mustChangePassword,
        UserStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? avatarUrl = null,
        string? contactNumber = null,
        string? pendingEmail = null,
        bool mustCompleteProfile = false)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new DomainException("First name must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new DomainException("Last name must not be empty.");
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email;
        PasswordHash = passwordHash;
        EmailVerified = emailVerified;
        MustChangePassword = mustChangePassword;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        AvatarUrl = avatarUrl;
        ContactNumber = contactNumber;
        PendingEmail = pendingEmail;
        MustCompleteProfile = mustCompleteProfile;
    }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    /// <summary>Convenience for display and outbound email - never stored, always derived.</summary>
    public string FullName => $"{FirstName} {LastName}";

    public Email Email { get; private set; }

    /// <summary>
    /// Null for Google-only accounts. Callers must treat a null hash as "this account has no
    /// password", never as "any password matches" - see <see cref="HasPassword"/>.
    /// </summary>
    public string? PasswordHash { get; private set; }

    public bool EmailVerified { get; private set; }

    public bool MustChangePassword { get; private set; }

    /// <summary>
    /// True for a Google-registered account that has not yet supplied a phone number - Google
    /// cannot provide one through the ID token, so the account is gated the same way a forced
    /// password reset is (limited-scope token, single reachable endpoint) until it does.
    /// </summary>
    public bool MustCompleteProfile { get; private set; }

    public UserStatus Status { get; private set; }

    public string? AvatarUrl { get; private set; }

    public string? ContactNumber { get; private set; }

    /// <summary>Address awaiting verification. <see cref="Email"/> stays authoritative until then.</summary>
    public string? PendingEmail { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<RoleName> Roles => _roles;

    public IReadOnlyCollection<AuthProvider> AuthProviders => _authProviders;

    /// <summary>True when the account can take part in password-based flows at all (PRF-012).</summary>
    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);

    public bool IsDeleted => Status == UserStatus.Deleted;

    public bool IsSuspended => Status == UserStatus.Suspended;

    /// <summary>
    /// Creates a new account registered with email and password. Starts unverified: the
    /// verification email is what promotes it (EVR-001). The phone number is collected directly
    /// on the registration form, so the account never needs the profile-completion gate.
    /// </summary>
    public static User RegisterWithEmail(
        Guid id,
        string firstName,
        string lastName,
        Email email,
        string passwordHash,
        string contactNumber,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("A locally registered user must have a password hash.");
        }

        if (string.IsNullOrWhiteSpace(contactNumber))
        {
            throw new DomainException("A phone number is required to register.");
        }

        var user = new User(
            id,
            firstName,
            lastName,
            email,
            passwordHash,
            emailVerified: false,
            mustChangePassword: false,
            status: UserStatus.Active,
            createdAt: now,
            updatedAt: now,
            contactNumber: contactNumber);

        user.AddRole(RoleName.Student);
        return user;
    }

    /// <summary>
    /// Creates a new account registered through Google. Google has already proven the address,
    /// so this account skips our own verification step entirely (BR-REG-004). Google's ID token
    /// carries no phone number, so the account starts gated behind MustCompleteProfile until
    /// <see cref="CompleteProfile"/> supplies one.
    /// </summary>
    public static User RegisterWithGoogle(
        Guid id,
        string firstName,
        string lastName,
        Email email,
        DateTimeOffset now)
    {
        var user = new User(
            id,
            firstName,
            lastName,
            email,
            passwordHash: null,
            emailVerified: true,
            mustChangePassword: false,
            status: UserStatus.Active,
            createdAt: now,
            updatedAt: now,
            mustCompleteProfile: true);

        user.AddRole(RoleName.Student);
        return user;
    }

    public void AddRole(RoleName role)
    {
        if (_roles.Contains(role))
        {
            return;
        }

        _roles.Add(role);
    }

    public void AddAuthProvider(AuthProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (_authProviders.Any(existing => existing.Provider == provider.Provider))
        {
            return;
        }

        _authProviders.Add(provider);
    }

    public bool HasProvider(AuthProviderKind provider) =>
        _authProviders.Any(existing => existing.Provider == provider);

    /// <summary>
    /// Marks the address confirmed. A deleted account cannot become verified - that transition
    /// would resurrect a terminal account through a stale link.
    /// </summary>
    public void MarkEmailVerified(DateTimeOffset now)
    {
        if (Status == UserStatus.Deleted)
        {
            throw new DomainException("A deleted account cannot be verified.");
        }

        EmailVerified = true;
        Touch(now);
    }

    /// <summary>Sets a new password and clears the forced-reset flag (LOG-014).</summary>
    public void SetPassword(string passwordHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash must not be empty.");
        }

        if (Status == UserStatus.Deleted)
        {
            throw new DomainException("A deleted account cannot change its password.");
        }

        PasswordHash = passwordHash;
        MustChangePassword = false;
        Touch(now);
    }

    /// <summary>Forces the next sign-in through the mandatory change-password flow (LOG-012).</summary>
    public void RequirePasswordChange(DateTimeOffset now)
    {
        MustChangePassword = true;
        Touch(now);
    }

    /// <summary>
    /// Supplies the phone number a Google-registered account could not provide at signup and
    /// clears the profile-completion gate. The one route a profile_completion_required token
    /// may reach.
    /// </summary>
    public void CompleteProfile(string contactNumber, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(contactNumber))
        {
            throw new DomainException("A phone number is required to complete your profile.");
        }

        ContactNumber = contactNumber;
        MustCompleteProfile = false;
        Touch(now);
    }

    public void UpdateProfile(string firstName, string lastName, string? avatarUrl, string? contactNumber, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new DomainException("First name must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new DomainException("Last name must not be empty.");
        }

        // Role and verification status are intentionally untouched here (BR-PRF-005).
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        AvatarUrl = avatarUrl;
        ContactNumber = contactNumber;
        Touch(now);
    }

    /// <summary>Records that a change to this address is awaiting confirmation (BR-PRF-002).</summary>
    public void BeginEmailChange(Email newEmail, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(newEmail);

        if (newEmail.Equals(Email))
        {
            throw new DomainException("The new email address matches the current one.");
        }

        PendingEmail = newEmail.Value;
        Touch(now);
    }

    /// <summary>Promotes the pending address once its token has been confirmed.</summary>
    public void CompleteEmailChange(Email newEmail, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(newEmail);

        if (Status == UserStatus.Deleted)
        {
            throw new DomainException("A deleted account cannot change its email.");
        }

        Email = newEmail;
        PendingEmail = null;

        // The address changed, so the previous proof of ownership no longer applies to it.
        EmailVerified = true;
        Touch(now);
    }

    public void CancelEmailChange(DateTimeOffset now)
    {
        PendingEmail = null;
        Touch(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (Status == UserStatus.Deleted)
        {
            throw new DomainException("A deleted account cannot be deactivated.");
        }

        Status = UserStatus.Deactivated;
        Touch(now);
    }

    /// <summary>
    /// Brings a deactivated account back. Called from the login flow when a deactivated user
    /// authenticates successfully (BR-SET-005).
    /// </summary>
    public void Reactivate(DateTimeOffset now)
    {
        if (Status == UserStatus.Deleted)
        {
            throw new DomainException("A deleted account cannot be reactivated.");
        }

        Status = UserStatus.Active;
        Touch(now);
    }

    /// <summary>
    /// Admin-imposed suspension (a ban). Unlike <see cref="Deactivate"/> this has no self-service
    /// route back - only <see cref="LiftSuspension"/>, called by an admin, restores the account.
    /// The caller (SuspendUserCommandHandler) is responsible for the "already suspended /
    /// deactivated" guard, since that produces a specific user-facing error rather than a
    /// generic domain exception.
    /// </summary>
    public void Suspend(DateTimeOffset now)
    {
        if (Status == UserStatus.Deleted)
        {
            throw new DomainException("A deleted account cannot be suspended.");
        }

        Status = UserStatus.Suspended;
        Touch(now);
    }

    /// <summary>Restores a suspended account to active. Called only by an admin lifting a ban.</summary>
    public void LiftSuspension(DateTimeOffset now)
    {
        if (Status != UserStatus.Suspended)
        {
            throw new DomainException("This account is not currently suspended.");
        }

        Status = UserStatus.Active;
        Touch(now);
    }

    /// <summary>
    /// Terminal deletion. Clears the credentials and personal fields but leaves the row in
    /// place; audit history is retained separately and deliberately outlives this (BR-SET-004).
    /// </summary>
    public void MarkDeleted(DateTimeOffset now)
    {
        Status = UserStatus.Deleted;
        PasswordHash = null;
        AvatarUrl = null;
        ContactNumber = null;
        PendingEmail = null;
        MustChangePassword = false;
        MustCompleteProfile = false;
        Touch(now);
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = now;
}
