using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Archive;

/// <summary>Which axis the archive browse tree branches on.</summary>
public enum ArchiveTreeMode
{
    /// <summary>الدوائر — the OrgUnit hierarchy a document is owned by, then year, then type.</summary>
    Department = 0,
    /// <summary>المصدر — where the document entered the archive from: an external party through the
    /// Diwan register, an internal message, or filed directly. Both registers appear here because
    /// both file into the same Documents table; showing only one made the other look like it had
    /// been filed with no origin at all.</summary>
    External = 1,
}

/// <summary>One node of the archive browse tree. <paramref name="Id"/> is a parseable selector
/// (see <see cref="ArchiveNodeSelector"/>), so the client hands it straight back to the documents
/// endpoint instead of reconstructing filters from the node's position in the tree.</summary>
public sealed record ArchiveTreeNodeDto(
    string Id,
    string Name,
    string Kind,                                    // Department | Year | DocumentType | ExternalOrg | Direction | Internal
    int Count,                                      // documents here INCLUDING descendants
    string? Code = null,
    IReadOnlyList<ArchiveTreeNodeDto>? Children = null);

/// <summary>A row of the node-scoped archive table. Purpose-built rather than reusing
/// DocumentListItem: this view needs the origin columns (department, external party,
/// direction) and none of the physical-box columns.</summary>
public sealed record ArchiveTreeDocumentDto(
    long Id,
    string DocumentNumber,
    string Title,
    string DocumentTypeName,
    string DepartmentName,
    string Confidentiality,
    string Status,
    DateOnly? DocumentDate,
    DateTime CreatedAt,
    string? ExternalOrgName,                        // null when the document did not come from the register
    string? Direction,                              // Incoming | Outgoing, from the originating register entry
    /// <summary>Where this document entered the archive: External | Internal | Direct. Lets the
    /// table say which register a row came from without the caller inferring it from nulls.</summary>
    string Origin = "Direct");

/// <summary>The filter a tree node stands for, encoded into (and parsed back out of) the node id.
///
/// Format is `key:value` segments joined by `|`, so a node id is self-describing and stable:
///   <c>dept:5</c> · <c>dept:5|y:2026</c> · <c>dept:5|y:2026|t:3</c>
///   <c>ext:7</c> · <c>ext:7|dir:Incoming</c> · <c>ext:none</c> · <c>ext:direct</c>
///   <c>int:all</c> · <c>int:4</c> (internal messages, optionally narrowed to a sending department)
/// </summary>
public sealed record ArchiveNodeSelector(
    long? DepartmentId = null,
    int? Year = null,
    long? DocumentTypeId = null,
    long? ExternalOrgId = null,
    // Register entries carrying no external organization (filed under غير محدد).
    bool ExternalNone = false,
    // Documents with no originating register entry at all (filed under مباشر).
    bool DirectOnly = false,
    DiwanDirection? Direction = null,
    // Documents filed from an internal message; the id narrows to the sending department.
    bool InternalOnly = false,
    long? InternalOrgUnitId = null)
{
    public const string DepartmentPrefix = "dept";
    public const string ExternalPrefix = "ext";
    public const string InternalPrefix = "int";

    /// <summary>Empty selector = the whole archive (what the table shows before any node is picked).</summary>
    public static ArchiveNodeSelector All { get; } = new();

    public static string DepartmentId_(long id) => $"{DepartmentPrefix}:{id}";
    public static string YearId(long deptId, int year) => $"{DepartmentId_(deptId)}|y:{year}";
    public static string TypeId(long deptId, int year, long typeId) => $"{YearId(deptId, year)}|t:{typeId}";
    public static string ExternalId(long? orgId) => $"{ExternalPrefix}:{(orgId?.ToString() ?? "none")}";
    public static string DirectId => $"{ExternalPrefix}:direct";
    public static string DirectionId(long? orgId, DiwanDirection d) => $"{ExternalId(orgId)}|dir:{d}";
    public static string InternalAllId => $"{InternalPrefix}:all";
    public static string InternalUnitId(long? unitId) => $"{InternalPrefix}:{(unitId?.ToString() ?? "none")}";

    /// <summary>Parses a node id. Unknown or malformed input yields <see cref="All"/> rather than
    /// throwing — a stale node id from a bookmarked URL should widen to the whole archive, not 500.</summary>
    public static ArchiveNodeSelector Parse(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return All;

        var sel = All;
        foreach (var segment in nodeId.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = segment.IndexOf(':');
            if (i <= 0) continue;
            var key = segment[..i];
            var value = segment[(i + 1)..];

            switch (key)
            {
                case DepartmentPrefix when long.TryParse(value, out var deptId):
                    sel = sel with { DepartmentId = deptId };
                    break;
                case "y" when int.TryParse(value, out var year):
                    sel = sel with { Year = year };
                    break;
                case "t" when long.TryParse(value, out var typeId):
                    sel = sel with { DocumentTypeId = typeId };
                    break;
                case ExternalPrefix when value == "none":
                    sel = sel with { ExternalNone = true };
                    break;
                case ExternalPrefix when value == "direct":
                    sel = sel with { DirectOnly = true };
                    break;
                case ExternalPrefix when long.TryParse(value, out var orgId):
                    sel = sel with { ExternalOrgId = orgId };
                    break;
                case InternalPrefix when value == "all":
                    sel = sel with { InternalOnly = true };
                    break;
                case InternalPrefix when value == "none":
                    sel = sel with { InternalOnly = true, InternalOrgUnitId = null };
                    break;
                case InternalPrefix when long.TryParse(value, out var unitId):
                    sel = sel with { InternalOnly = true, InternalOrgUnitId = unitId };
                    break;
                case "dir" when Enum.TryParse<DiwanDirection>(value, out var dir):
                    sel = sel with { Direction = dir };
                    break;
            }
        }
        return sel;
    }
}
