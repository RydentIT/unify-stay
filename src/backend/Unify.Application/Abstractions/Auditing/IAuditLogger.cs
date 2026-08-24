namespace Unify.Application.Abstractions.Auditing;

/// <summary>
/// Records security-relevant events. Handlers call this; the implementation decides where
/// entries land (currently the audit_logs table).
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(
        string action,
        Guid? actorUserId = null,
        string? subjectType = null,
        string? subjectId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}
