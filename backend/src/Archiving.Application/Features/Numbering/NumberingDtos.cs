using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Numbering;

public sealed record NumberingTemplateDto(
    long Id, string EntityType, long? InstitutionId, long? OrgUnitId, long? DocumentTypeId,
    string Pattern, string ResetFrequency, int CurrentSequence, int? CurrentYear, int? CurrentMonth, bool IsActive);

public sealed record CreateNumberingTemplateDto(
    string EntityType, long? InstitutionId, long? OrgUnitId, long? DocumentTypeId,
    string Pattern, ResetFrequency ResetFrequency);
