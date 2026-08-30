using Unify.Domain.Common;

namespace Unify.Domain.Users;

public enum SuspensionStatus
{
    Active = 0,
    Lifted = 1,
}

/// <summary>
/// The record of one suspend action - who was suspended, by which admin, why, and (once lifted)
/// by whom and when. Whether the account is CURRENTLY suspended lives on <see cref="User.Status"/>;
/// this is the audit-shaped detail behind that status, not a second source of truth for it.
/// </summary>
public sealed class UserSuspension : Entity
{
    public UserSuspension(
        Guid id,
        Guid userId,
        string reason,
        Guid suspendedBy,
        DateTimeOffset suspendedAt,
        SuspensionStatus status,
        Guid? liftedBy = null,
        DateTimeOffset? liftedAt = null)
        : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A suspension must belong to a user.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A suspension requires a reason.");
        }

        if (suspendedBy == Guid.Empty)
        {
            throw new DomainException("A suspension must record the admin who imposed it.");
        }

        if (status == SuspensionStatus.Lifted && (liftedBy is null || liftedAt is null))
        {
            throw new DomainException("A lifted suspension must record who lifted it and when.");
        }

        UserId = userId;
        Reason = reason.Trim();
        SuspendedBy = suspendedBy;
        SuspendedAt = suspendedAt;
        Status = status;
        LiftedBy = liftedBy;
        LiftedAt = liftedAt;
    }

    public Guid UserId { get; }

    public string Reason { get; }

    public Guid SuspendedBy { get; }

    public DateTimeOffset SuspendedAt { get; }

    public SuspensionStatus Status { get; private set; }

    public Guid? LiftedBy { get; private set; }

    public DateTimeOffset? LiftedAt { get; private set; }

    public bool IsActive => Status == SuspensionStatus.Active;

    public static UserSuspension Impose(Guid id, Guid userId, string reason, Guid suspendedBy, DateTimeOffset now) =>
        new(id, userId, reason, suspendedBy, now, SuspensionStatus.Active);

    public void Lift(Guid liftedBy, DateTimeOffset now)
    {
        if (Status != SuspensionStatus.Active)
        {
            throw new DomainException("This suspension has already been lifted.");
        }

        if (liftedBy == Guid.Empty)
        {
            throw new DomainException("Lifting a suspension must record the admin who lifted it.");
        }

        Status = SuspensionStatus.Lifted;
        LiftedBy = liftedBy;
        LiftedAt = now;
    }
}
