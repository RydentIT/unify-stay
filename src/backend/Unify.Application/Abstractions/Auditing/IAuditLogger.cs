namespace Unify.Application.Abstractions.Auditing;

/// <summary>
/// Describes one auditable action. Every field beyond the action is optional because the specs
/// ask for different context per module: registration records the provider, login records the
/// IP, profile edits record which fields changed.
/// </summary>
public sealed record AuditEvent
{
    public required string ActionType { get; init; }

    public Guid? UserId { get; init; }

    public string? Email { get; init; }

    public string? Provider { get; init; }

    public string? IpAddress { get; init; }

    /// <summary>Serialised to JSON by the implementation.</summary>
    public object? FieldsChanged { get; init; }
}

/// <summary>
/// Records security-relevant actions. Implementations must not let a logging failure break the
/// user-facing operation that triggered it.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
