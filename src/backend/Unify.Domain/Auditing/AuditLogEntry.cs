using Unify.Domain.Common;

namespace Unify.Domain.Auditing;

/// <summary>
/// An append-only record of a security-relevant action. Never updated or deleted by
/// application code - the table grants no UPDATE/DELETE to the app role.
/// </summary>
public sealed class AuditLogEntry : Entity
{
    public AuditLogEntry(
        Guid id,
        string action,
        Guid? actorUserId,
        string? subjectType,
        string? subjectId,
        string? ipAddress,
        string? userAgent,
        string? metadataJson,
        DateTimeOffset occurredAtUtc)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new DomainException("Audit log action must not be empty.");
        }

        Action = action;
        ActorUserId = actorUserId;
        SubjectType = subjectType;
        SubjectId = subjectId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        MetadataJson = metadataJson;
        OccurredAtUtc = occurredAtUtc;
    }

    /// <summary>Dotted event name, e.g. "user.register.succeeded".</summary>
    public string Action { get; }

    /// <summary>Null for anonymous actions such as a failed login on an unknown address.</summary>
    public Guid? ActorUserId { get; }

    public string? SubjectType { get; }

    public string? SubjectId { get; }

    public string? IpAddress { get; }

    public string? UserAgent { get; }

    public string? MetadataJson { get; }

    public DateTimeOffset OccurredAtUtc { get; }
}
