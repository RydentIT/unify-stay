using Unify.Domain.Common;

namespace Unify.Domain.Users;

/// <summary>
/// A way a given user can authenticate. One row per (user, provider) pair, so a single
/// account can hold both a local password and one or more federated logins.
/// </summary>
public sealed class AuthProvider : Entity
{
    public AuthProvider(
        Guid id,
        Guid userId,
        AuthProviderKind kind,
        string providerSubject,
        DateTimeOffset linkedAtUtc)
        : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("AuthProvider must belong to a user.");
        }

        if (string.IsNullOrWhiteSpace(providerSubject))
        {
            throw new DomainException("AuthProvider subject must not be empty.");
        }

        UserId = userId;
        Kind = kind;
        ProviderSubject = providerSubject;
        LinkedAtUtc = linkedAtUtc;
    }

    public Guid UserId { get; }

    public AuthProviderKind Kind { get; }

    /// <summary>
    /// The provider's stable identifier for this user (the OIDC "sub"). For
    /// <see cref="AuthProviderKind.Local"/> this is the user's own id.
    /// </summary>
    public string ProviderSubject { get; }

    public DateTimeOffset LinkedAtUtc { get; }
}
