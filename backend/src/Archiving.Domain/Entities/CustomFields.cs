using Archiving.Domain.Common;

namespace Archiving.Domain.Entities;

public enum CustomFieldType { Text = 0, Number = 1, Date = 2, Choice = 3 }

/// <summary>An admin-defined custom field for a record type (Document, IncomingMail, …).</summary>
public class CustomFieldDefinition : BaseEntity
{
    public string EntityType { get; set; } = "Document";   // Document | IncomingMail | OutgoingMail | ArchiveItem
    public string FieldKey { get; set; } = string.Empty;    // stable slug, auto-generated
    public string Label { get; set; } = string.Empty;       // display name (admin-editable)
    public CustomFieldType FieldType { get; set; }
    public string? Options { get; set; }                    // newline/CSV-separated choices (Choice type)
    public bool Searchable { get; set; } = true;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A value for a custom field on a specific record.</summary>
public class CustomFieldValue : BaseEntity
{
    public long FieldId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public string Value { get; set; } = string.Empty;
}
