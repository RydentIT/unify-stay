using System.Text.Json;
using Microsoft.Extensions.Logging;
using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Time;
using Unify.Domain.Auditing;

namespace Unify.Infrastructure.Auditing;

/// <summary>
/// Writes audit entries to the database.
///
/// Failures are logged and swallowed: an audit write must never be the reason a user-facing
/// operation fails. The trade-off is explicit - if an action ever needs a guaranteed trail, it
/// should write the entry inside its own transaction rather than through this logger.
/// </summary>
internal sealed class AuditLogger : IAuditLogger
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAuditLogRepository _repository;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(
        IAuditLogRepository repository,
        IDateTimeProvider clock,
        ILogger<AuditLogger> logger)
    {
        _repository = repository;
        _clock = clock;
        _logger = logger;
    }

    public async Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var entry = new AuditLogEntry(
            Guid.CreateVersion7(),
            auditEvent.ActionType,
            _clock.UtcNow,
            auditEvent.UserId,
            auditEvent.Email,
            auditEvent.Provider,
            auditEvent.IpAddress,
            auditEvent.FieldsChanged is null
                ? null
                : JsonSerializer.Serialize(auditEvent.FieldsChanged, MetadataJsonOptions));

        try
        {
            await _repository.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Failed to write audit entry for action {ActionType}.",
                auditEvent.ActionType);
        }
    }
}
