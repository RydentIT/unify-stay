using Unify.Domain.Common;

namespace Unify.Domain.Users;

/// <summary>
/// A way a given user can authenticate. One row per (user, provider) pair, so an account can
/// hold both a local password and a linked Google identity - which is what REG-004 produces.
/// </summary>
public sealed class AuthProvider : Entity
{
    public AuthProvider(
        Guid id,
        Guid userId,
        AuthProviderKind provider,
        string providerUserId,
        bool emailVerifiedByProvider,
        DateTimeOffset linkedAt)
        : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("AuthProvider must belong to a user.");
        }

        if (string.IsNullOrWhiteSpace(providerUserId))
        {
            throw new DomainException("AuthProvider subject must not be empty.");
        }

        UserId = userId;
        Provider = provider;
        ProviderUserId = providerUserId;
        EmailVerifiedByProvider = emailVerifiedByProvider;
        LinkedAt = linkedAt;
    }

    public Guid UserId { get; }

    public AuthProviderKind Provider { get; }

    /// <summary>The provider's stable subject. For <see cref="AuthProviderKind.Local"/> this is the user id.</summary>
    public string ProviderUserId { get; }

    /// <summary>
    /// Whether the provider itself vouched for the address. Only ever set from a server-side
    /// verified ID token, never from a client assertion - it decides REG-004 vs REG-005.
    /// </summary>
    public bool EmailVerifiedByProvider { get; }

    public DateTimeOffset LinkedAt { get; }

    public static AuthProvider Local(Guid id, Guid userId, DateTimeOffset now) =>
        new(id, userId, AuthProviderKind.Local, userId.ToString(), emailVerifiedByProvider: false, now);

    public static AuthProvider Google(
        Guid id,
        Guid userId,
        string googleSubject,
        bool emailVerified,
        DateTimeOffset now) =>
        new(id, userId, AuthProviderKind.Google, googleSubject, emailVerified, now);
}
