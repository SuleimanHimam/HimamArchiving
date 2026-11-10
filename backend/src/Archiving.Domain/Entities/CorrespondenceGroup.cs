using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>A named distribution group of employees and/or departments. Sending internal
/// correspondence to a group fans out one tracked incoming item to each member.</summary>
public class CorrespondenceGroup : BaseEntity
{
    public long InstitutionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CorrespondenceGroupMember> Members { get; set; } = new List<CorrespondenceGroupMember>();
}

public class CorrespondenceGroupMember : BaseEntity
{
    public long GroupId { get; set; }
    public GroupMemberType MemberType { get; set; }
    public long? UserId { get; set; }        // set when MemberType = User
    public long? OrgUnitId { get; set; }     // set when MemberType = OrgUnit

    public CorrespondenceGroup Group { get; set; } = null!;
}
