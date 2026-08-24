using Unify.Domain.Auditing;

namespace Unify.Application.Abstractions.Persistence;

/// <summary>Append-only sink for <see cref="AuditLogEntry"/>. No update or delete by design.</summary>
public interface IAuditLogRepository
{
    Task AppendAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}
