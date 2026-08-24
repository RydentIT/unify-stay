using System.Text.Json;
using Microsoft.Extensions.Logging;
using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Time;
using Unify.Domain.Auditing;

namespace Unify.Infrastructure.Auditing;

/// <summary>
/// Writes audit entries to the database. Failures are logged and swallowed: an audit write
/// must never be the reason a user-facing operation fails. If that trade-off ever becomes
/// unacceptable for a given action, that action should write the entry in its own transaction.
/// </summary>
internal sealed class AuditLogger : IAuditLogger
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAuditLogRepository _repository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(
        IAuditLogRepository repository,
        IDateTimeProvider dateTimeProvider,
        ILogger<AuditLogger> logger)
    {
        _repository = repository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        Guid? actorUserId = null,
        string? subjectType = null,
        string? subjectId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditLogEntry(
            Guid.CreateVersion7(),
            action,
            actorUserId,
            subjectType,
            subjectId,
            ipAddress: null,
            userAgent: null,
            metadataJson: metadata is null
                ? null
                : JsonSerializer.Serialize(metadata, MetadataJsonOptions),
            occurredAtUtc: _dateTimeProvider.UtcNow);

        try
        {
            await _repository.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to write audit entry for action {Action}.", action);
        }
    }
}
