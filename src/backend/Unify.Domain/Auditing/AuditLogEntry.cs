using Unify.Domain.Common;

namespace Unify.Domain.Auditing;

/// <summary>
/// An append-only record of a security-relevant action.
///
/// Every field except the action and timestamp is nullable by design: an entry may describe an
/// anonymous attempt (failed login on an unknown address), and the email/provider are captured
/// as values rather than as a foreign key so the trail survives account deletion (BR-SET-004).
/// </summary>
public sealed class AuditLogEntry : Entity
{
    public AuditLogEntry(
        Guid id,
        string actionType,
        DateTimeOffset occurredAt,
        Guid? userId = null,
        string? email = null,
        string? provider = null,
        string? ipAddress = null,
        string? fieldsChanged = null)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            throw new DomainException("Audit log action type must not be empty.");
        }

        ActionType = actionType;
        OccurredAt = occurredAt;
        UserId = userId;
        Email = email;
        Provider = provider;
        IpAddress = ipAddress;
        FieldsChanged = fieldsChanged;
    }

    /// <summary>Dotted event name, e.g. "user.login.locked".</summary>
    public string ActionType { get; }

    public Guid? UserId { get; }

    public string? Email { get; }

    public string? Provider { get; }

    public string? IpAddress { get; }

    /// <summary>JSON naming what changed, where the relevant spec calls for it.</summary>
    public string? FieldsChanged { get; }

    public DateTimeOffset OccurredAt { get; }
}
