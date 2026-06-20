namespace Archiving.Application.Common.Interfaces;

/// <summary>Drives full-text extraction over stored attachments (used by the background worker and admin reindex).</summary>
public interface ITextIndexingService
{
    /// <summary>Process up to <paramref name="batch"/> attachments still pending extraction. Returns how many were processed.</summary>
    Task<int> SweepPendingAsync(int batch, CancellationToken ct = default);

    /// <summary>Re-queue every attachment for extraction (clears existing text). Returns how many were queued.</summary>
    Task<int> ReindexAllAsync(CancellationToken ct = default);
}
