using Archiving.Application.Common.Models;
using Archiving.Application.Features.Core;

namespace Archiving.Application.Features.Hr;

// RowVersion travels as base64: ignored on create, mandatory on update (same rule as the core DTOs).

// ---- Settings: personnel action types ----

public sealed record ActionTypeDto(long Id, string Code, string Name, string Effect, long? WorkflowDefinitionId,
    string? WorkflowDefinitionName, bool IsActive, string RowVersion);

public sealed record SaveActionTypeRequest(string Code, string Name, string Effect, long? WorkflowDefinitionId,
    bool IsActive = true, string? RowVersion = null);

public interface IActionTypeService
{
    Task<IReadOnlyList<ActionTypeDto>> ListAsync(bool activeOnly, CancellationToken ct = default);
    Task<Result<ActionTypeDto>> CreateAsync(SaveActionTypeRequest request, CancellationToken ct = default);
    Task<Result<ActionTypeDto>> UpdateAsync(long id, SaveActionTypeRequest request, CancellationToken ct = default);
}

// ---- Employee file ----

public sealed record ProfileDto(DateOnly? BirthDate, long? MaritalStatusId, string? Address,
    long? AppointmentTypeId, long? GradeId, string? NationalIdMasked, string? MobileMasked, string? RowVersion);

/// <summary>
/// NationalId / Mobile: null keeps the stored value, an empty string clears it.
/// AppointmentTypeId / GradeId are accepted only while unset (launch data); afterwards they change
/// through a Confirm or Promote action.
/// </summary>
public sealed record SaveProfileRequest(DateOnly? BirthDate, long? MaritalStatusId, string? Address,
    long? AppointmentTypeId, long? GradeId, string? NationalId, string? Mobile, string? RowVersion = null);

public sealed record FamilyMemberDto(long Id, string FullName, long RelationshipId, DateOnly? BirthDate, string? Notes, string RowVersion);
public sealed record SaveFamilyMemberRequest(string FullName, long RelationshipId, DateOnly? BirthDate, string? Notes, string? RowVersion = null);

public sealed record QualificationDto(long Id, long QualificationTypeId, string Title, string? Institution, int? Year, string RowVersion);
public sealed record SaveQualificationRequest(long QualificationTypeId, string Title, string? Institution, int? Year, string? RowVersion = null);

public sealed record BankAccountDto(long Id, string BankName, string AccountMasked, bool IsPrimary, string RowVersion);
/// <summary>Account is required on create; on update null keeps the stored value.</summary>
public sealed record SaveBankAccountRequest(string BankName, string? Account, bool IsPrimary = true, string? RowVersion = null);

public sealed record EmployeeFileDto(EmployeeDto Employee, ProfileDto Profile,
    IReadOnlyList<FamilyMemberDto> Family, IReadOnlyList<QualificationDto> Qualifications,
    IReadOnlyList<BankAccountDto> BankAccounts, IReadOnlyList<PersonnelActionDto> Actions);

public static class SensitiveFields
{
    public const string NationalId = "nationalId";
    public const string Mobile = "mobile";
    public const string BankAccount = "bankAccount";
}

/// <summary>RecordId is the bank-account id for <see cref="SensitiveFields.BankAccount"/>; ignored otherwise.</summary>
public sealed record RevealRequest(string Field, long? RecordId = null);
public sealed record RevealDto(string Value);

public interface IEmployeeFileService
{
    Task<Result<EmployeeFileDto>> GetAsync(long employeeId, CancellationToken ct = default);
    Task<Result<ProfileDto>> SaveProfileAsync(long employeeId, SaveProfileRequest request, CancellationToken ct = default);

    Task<Result<FamilyMemberDto>> AddFamilyMemberAsync(long employeeId, SaveFamilyMemberRequest request, CancellationToken ct = default);
    Task<Result<FamilyMemberDto>> UpdateFamilyMemberAsync(long employeeId, long id, SaveFamilyMemberRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteFamilyMemberAsync(long employeeId, long id, CancellationToken ct = default);

    Task<Result<QualificationDto>> AddQualificationAsync(long employeeId, SaveQualificationRequest request, CancellationToken ct = default);
    Task<Result<QualificationDto>> UpdateQualificationAsync(long employeeId, long id, SaveQualificationRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteQualificationAsync(long employeeId, long id, CancellationToken ct = default);

    Task<Result<BankAccountDto>> AddBankAccountAsync(long employeeId, SaveBankAccountRequest request, CancellationToken ct = default);
    Task<Result<BankAccountDto>> UpdateBankAccountAsync(long employeeId, long id, SaveBankAccountRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteBankAccountAsync(long employeeId, long id, CancellationToken ct = default);

    /// <summary>Returns one sensitive value in clear and writes an audit entry (never the value).</summary>
    Task<Result<RevealDto>> RevealAsync(long employeeId, RevealRequest request, CancellationToken ct = default);
}

// ---- Personnel actions ----

/// <summary>
/// The target values of an action. Which fields are read depends on the type's effect:
/// Hire → EmployeeNumber, FullName, FullNameEn, Category, OrgUnitId, PositionId, JobTitle, SiteId, GradeId, AppointmentTypeId;
/// Transfer → OrgUnitId (required), PositionId, JobTitle, SiteId; Promote → GradeId; Confirm → AppointmentTypeId;
/// Second → SiteId, ToDate; Allowance, Penalty, Suspend, EndService → Notes.
/// </summary>
public sealed record PersonnelActionPayload(
    string? EmployeeNumber = null, string? FullName = null, string? FullNameEn = null, string? Category = null,
    long? OrgUnitId = null, long? PositionId = null, string? JobTitle = null, long? SiteId = null,
    long? GradeId = null, long? AppointmentTypeId = null, DateOnly? ToDate = null, string? Notes = null);

public sealed record PersonnelActionDto(long Id, string? Number, long ActionTypeId, string ActionTypeName, string Effect,
    long? EmployeeId, string? EmployeeName, string Status, DateOnly EffectiveDate, string Reason,
    PersonnelActionPayload Payload, string? BeforeJson, string? AfterJson, long? ApprovalRequestId,
    DateTime? ExecutedAt, string? DecisionNote, DateTime CreatedAt, string RowVersion);

/// <summary>EmployeeId is required for every effect except Hire, where it must be null.</summary>
public sealed record SavePersonnelActionRequest(long ActionTypeId, long? EmployeeId, DateOnly EffectiveDate,
    string Reason, PersonnelActionPayload Payload, string? RowVersion = null);

public sealed record PersonnelActionListQuery(long? EmployeeId = null, string? Status = null, int Page = 1, int PageSize = 20);

public interface IPersonnelActionService
{
    Task<PagedResult<PersonnelActionDto>> ListAsync(PersonnelActionListQuery query, CancellationToken ct = default);
    Task<Result<PersonnelActionDto>> GetAsync(long id, CancellationToken ct = default);
    Task<Result<PersonnelActionDto>> CreateAsync(SavePersonnelActionRequest request, CancellationToken ct = default);
    Task<Result<PersonnelActionDto>> UpdateAsync(long id, SavePersonnelActionRequest request, CancellationToken ct = default);
    /// <summary>Sends a draft (or returned) action into its type's approval path.</summary>
    Task<Result<PersonnelActionDto>> SubmitAsync(long id, CancellationToken ct = default);
    Task<Result<PersonnelActionDto>> CancelAsync(long id, CancellationToken ct = default);
}

// ---- Clearance ----

public sealed record ClearanceItemDto(long Id, long PartyId, string PartyName, bool IsCleared, DateTime? ClearedAt, string? Note);

public sealed record ClearanceDto(long Id, string Number, long EmployeeId, string? EmployeeName, string Status,
    DateTime? IssuedAt, IReadOnlyList<ClearanceItemDto> Items);

public sealed record ClearItemRequest(string? Note);

public interface IClearanceService
{
    Task<IReadOnlyList<ClearanceDto>> ListAsync(string? status, CancellationToken ct = default);
    Task<Result<ClearanceDto>> GetAsync(long id, CancellationToken ct = default);
    /// <summary>Marks one party's item cleared. When the last item clears, the certificate is issued
    /// and the employee's login is disabled.</summary>
    Task<Result<ClearanceDto>> ClearItemAsync(long clearanceId, long itemId, ClearItemRequest request, CancellationToken ct = default);
}
