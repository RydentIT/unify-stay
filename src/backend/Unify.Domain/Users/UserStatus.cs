namespace Unify.Domain.Users;

public enum UserStatus
{
    /// <summary>Registered but the email address has not been verified yet.</summary>
    PendingVerification = 0,
    Active = 1,
    Suspended = 2,
    Deactivated = 3,
}
