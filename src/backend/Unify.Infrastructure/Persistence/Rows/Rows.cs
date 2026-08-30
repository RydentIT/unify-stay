namespace Unify.Infrastructure.Persistence.Rows;

/// <summary>
/// Flat projections of the tables. Dapper materialises these and the repositories map them to
/// domain objects, so the domain never has to expose setters or a parameterless constructor
/// purely to satisfy a data mapper.
/// </summary>
internal sealed class UserRow
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PasswordHash { get; init; }
    public bool EmailVerified { get; init; }
    public bool MustChangePassword { get; init; }
    public bool MustCompleteProfile { get; init; }
    public string Status { get; init; } = "active";
    public string? AvatarUrl { get; init; }
    public string? ContactNumber { get; init; }
    public string? PendingEmail { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

internal sealed class AuthProviderRow
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string ProviderUserId { get; init; } = string.Empty;
    public bool EmailVerifiedByProvider { get; init; }
    public DateTimeOffset LinkedAt { get; init; }
}

internal sealed class SessionRow
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string RefreshTokenHash { get; init; } = string.Empty;
    public string? RoleClaim { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

internal class TokenRow
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string TokenHash { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

internal sealed class PendingEmailChangeRow : TokenRow
{
    public string NewEmail { get; init; } = string.Empty;
}

internal sealed class UserSuspensionRow
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public Guid SuspendedBy { get; init; }
    public DateTimeOffset SuspendedAt { get; init; }
    public string Status { get; init; } = "active";
    public Guid? LiftedBy { get; init; }
    public DateTimeOffset? LiftedAt { get; init; }
}

/// <summary>One row of the admin directory listing - see <c>IUserRepository.SearchDirectoryAsync</c>.</summary>
internal sealed class UserDirectoryRow
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Status { get; init; } = "active";
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Comma-joined role names for this user, aggregated in SQL.</summary>
    public string? RoleNames { get; init; }
}

internal sealed class UpgradeRequestRow
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string NicNumber { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string PhoneNumber2 { get; init; } = string.Empty;
    public string PropertyInfo { get; init; } = string.Empty;
    public string Status { get; init; } = "pending";
    public string? RejectionReason { get; init; }
    public DateTimeOffset SubmittedAt { get; init; }
    public DateTimeOffset? DecidedAt { get; init; }
    public Guid? DecidedBy { get; init; }
}
