using Archiving.Domain.Common;

namespace Archiving.Domain.Entities;

/// <summary>The kind of agent acting on a record (ISO 23081 agent entity).</summary>
public enum AgentKind { User = 0, Position = 1, OrgUnit = 2 }

/// <summary>The role an agent plays in relation to a record.</summary>
public enum AgentRole { Creator = 0, Owner = 1, Custodian = 2, Contributor = 3, Approver = 4, Recipient = 5 }

/// <summary>A typed relationship between two records/entities (ISO 23081 relationship entity).</summary>
public enum RecordRelationshipType { IsVersionOf = 0, References = 1, Supersedes = 2, RespondsTo = 3, PartOf = 4, Attachment = 5 }

/// <summary>Links a record to an agent (person/seat/unit) with a role — ISO 23081 record↔agent metadata.</summary>
public class RecordAgent : BaseEntity
{
    public string RecordType { get; set; } = string.Empty;   // "Document" | "IncomingMail" | …
    public long RecordId { get; set; }
    public AgentKind AgentKind { get; set; }
    public long AgentId { get; set; }
    public AgentRole Role { get; set; }
}

/// <summary>A typed link between two records (or a record and a business activity) — ISO 23081 relationships.</summary>
public class RecordRelationship : BaseEntity
{
    public string SourceType { get; set; } = string.Empty;
    public long SourceId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public long TargetId { get; set; }
    public RecordRelationshipType Type { get; set; }
}
