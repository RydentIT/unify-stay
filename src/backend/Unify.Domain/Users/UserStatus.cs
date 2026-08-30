namespace Unify.Domain.Users;

/// <summary>
/// Lifecycle state of an account. Persisted by name (lower-cased) in users.status, guarded by
/// a CHECK constraint, so reordering this enum cannot silently reinterpret existing rows.
/// </summary>
public enum UserStatus
{
    Active = 0,

    /// <summary>
    /// Self-service suspension. The account still exists and the user can bring it back simply
    /// by logging in successfully (BR-SET-005).
    /// </summary>
    Deactivated = 1,

    /// <summary>
    /// Terminal. Personal data is cleared but the row is retained so audit entries and foreign
    /// keys stay coherent (BR-SET-004). There is no route back from here.
    /// </summary>
    Deleted = 2,

    /// <summary>
    /// Admin-imposed (a ban, per product decision "suspended" = "banned"). Rejected at login
    /// outright, distinct from <see cref="Deactivated"/> which reactivates on a successful
    /// login. Only an admin lifting the suspension brings the account back - see
    /// <see cref="User.Suspend"/> / <see cref="User.LiftSuspension"/>.
    /// </summary>
    Suspended = 3,
}
