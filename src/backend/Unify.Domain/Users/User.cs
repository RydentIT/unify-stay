using Unify.Domain.Common;

namespace Unify.Domain.Users;

/// <summary>
/// A registered account. Scaffolding only: this type currently holds invariants and shape,
/// not the Register/Login behaviour, which lands with the auth module.
/// </summary>
public sealed class User : Entity
{
    private readonly List<UserRole> _roles = [];
    private readonly List<AuthProvider> _authProviders = [];

    public User(
        Guid id,
        Email email,
        string? passwordHash,
        string displayName,
        UserStatus status,
        bool mustChangePassword,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? emailVerifiedAtUtc = null)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("Display name must not be empty.");
        }

        Email = email;
        PasswordHash = passwordHash;
        DisplayName = displayName.Trim();
        Status = status;
        MustChangePassword = mustChangePassword;
        CreatedAtUtc = createdAtUtc;
        EmailVerifiedAtUtc = emailVerifiedAtUtc;
    }

    public Email Email { get; private set; }

    /// <summary>
    /// Null for accounts that only ever authenticated through an external provider, so a
    /// missing hash is a valid state and must never be treated as "any password matches".
    /// </summary>
    public string? PasswordHash { get; private set; }

    public string DisplayName { get; private set; }

    public UserStatus Status { get; private set; }

    /// <summary>
    /// Drives the forced-reset flow: while true the user may only be issued a limited-scope
    /// token that unlocks nothing except the change-password endpoint.
    /// </summary>
    public bool MustChangePassword { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? EmailVerifiedAtUtc { get; private set; }

    public bool IsEmailVerified => EmailVerifiedAtUtc is not null;

    public IReadOnlyCollection<UserRole> Roles => _roles;

    public IReadOnlyCollection<AuthProvider> AuthProviders => _authProviders;

    public void AddRole(UserRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (_roles.Any(existing => existing.Role == role.Role))
        {
            return;
        }

        _roles.Add(role);
    }

    public void AddAuthProvider(AuthProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _authProviders.Add(provider);
    }
}
