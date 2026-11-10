using Archiving.Domain.Common;

namespace Archiving.Domain.Entities;

public enum CustomFieldType { Text = 0, Number = 1, Date = 2, Choice = 3 }

/// <summary>An admin-defined custom field for a record type (Document, IncomingMail, …).
/// Document fields may be narrowed to a single <see cref="DocumentType"/>, so an invoice can
/// carry an amount and a contract a party name without either polluting the other.</summary>
public class CustomFieldDefinition : BaseEntity
{
    public string EntityType { get; set; } = "Document";   // Document | IncomingMail | OutgoingMail | ArchiveItem
    /// <summary>Narrows the field to one document type. null = shared by every type of this record.
    /// Only meaningful when <see cref="EntityType"/> is "Document".</summary>
    public long? DocumentTypeId { get; set; }
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
