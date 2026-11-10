using Archiving.Domain.Enums;

namespace Archiving.Domain.StateMachine;

/// <summary>
/// The document lifecycle state machine. Encodes which <see cref="DocumentStatus"/> transitions are legal and
/// the permission each requires, so no API call or event handler can drive a document into an invalid state.
/// Adapted to the project's existing <see cref="DocumentStatus"/> values (Draft, Active, Archived,
/// PendingArchive, PendingDisposal, Disposed).
/// </summary>
public static class DocumentLifecycle
{
    // (From, To) → required permission.
    private static readonly Dictionary<(DocumentStatus, DocumentStatus), string> AllowedTransitions = new()
    {
        { (DocumentStatus.Draft,          DocumentStatus.Active),          "Documents.Edit" },
        { (DocumentStatus.Active,         DocumentStatus.PendingArchive),  "Documents.Archive" },   // submit to Archive dept
        { (DocumentStatus.Active,         DocumentStatus.Archived),        "Documents.Archive" },   // direct archive
        { (DocumentStatus.PendingArchive, DocumentStatus.Archived),        "Documents.Archive" },   // archive dept files it
        { (DocumentStatus.PendingArchive, DocumentStatus.Active),          "Documents.Edit" },      // returned to uploader
        { (DocumentStatus.Archived,       DocumentStatus.PendingDisposal), "Disposition.Create" },
        { (DocumentStatus.Archived,       DocumentStatus.Active),          "Documents.Edit" },      // reactivate
        { (DocumentStatus.PendingDisposal,DocumentStatus.Disposed),        "Disposition.Approve" },
        { (DocumentStatus.PendingDisposal,DocumentStatus.Archived),        "Disposition.Approve" }, // disposition rejected → back to archived
    };

    public static bool CanTransition(DocumentStatus from, DocumentStatus to) =>
        from == to || AllowedTransitions.ContainsKey((from, to));

    public static string? GetRequiredPermission(DocumentStatus from, DocumentStatus to) =>
        AllowedTransitions.TryGetValue((from, to), out var perm) ? perm : null;

    public static IReadOnlyList<DocumentStatus> GetAllowedNextStatuses(DocumentStatus current) =>
        AllowedTransitions.Keys.Where(k => k.Item1 == current).Select(k => k.Item2).ToList();
}
