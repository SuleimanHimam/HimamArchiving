namespace Archiving.Application.Common.Interfaces;

/// <summary>Appends tamper-evident (hash-chained) entries to the audit trail.</summary>
public interface IAuditWriter
{
    Task WriteAsync(
        string action,
        string entityType,
        long entityId,
        string? entityTitle = null,
        string? oldValues = null,
        string? newValues = null,
        CancellationToken ct = default);
}
