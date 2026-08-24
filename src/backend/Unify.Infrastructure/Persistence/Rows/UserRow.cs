namespace Unify.Infrastructure.Persistence.Rows;

/// <summary>
/// Flat projection of the users table. Dapper materialises these and the repository maps
/// them to domain objects, so the domain never has to expose a parameterless constructor or
/// settable properties just to satisfy a data mapper.
/// </summary>
internal sealed class UserRow
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string? PasswordHash { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public short Status { get; init; }

    public bool MustChangePassword { get; init; }

    public DateTimeOffset? EmailVerifiedAtUtc { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}

internal sealed class UserRoleRow
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string Role { get; init; } = string.Empty;

    public DateTimeOffset GrantedAtUtc { get; init; }
}

internal sealed class AuthProviderRow
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public short Kind { get; init; }

    public string ProviderSubject { get; init; } = string.Empty;

    public DateTimeOffset LinkedAtUtc { get; init; }
}
