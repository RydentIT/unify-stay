using Unify.Domain.Auditing;

namespace Unify.Application.Abstractions.Persistence;

/// <summary>Append-only sink. Offers no update or delete by design (BR-SET-004).</summary>
public interface IAuditLogRepository
{
    Task AppendAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogEntry>> ListForUserAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);
}
