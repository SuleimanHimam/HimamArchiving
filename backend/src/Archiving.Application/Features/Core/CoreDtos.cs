using Archiving.Application.Common.Models;

namespace Archiving.Application.Features.Core;

// RowVersion travels as base64. It is ignored on create and mandatory on update: an update is rejected
// when the token is missing or when the row has changed since the caller read it.

public sealed record SiteDto(long Id, string Name, string? NameEn, string Code, bool IsActive, string RowVersion);

public sealed record SaveSiteRequest(string Name, string? NameEn, string Code, bool IsActive = true, string? RowVersion = null);

public sealed record SiteLocationDto(
    long Id, long SiteId, string Name, string? NameEn, string Code, string Kind, bool IsActive, string RowVersion);

public sealed record SaveSiteLocationRequest(
    string Name, string? NameEn, string Code, string Kind, bool IsActive = true, string? RowVersion = null);

public sealed record EmployeeDto(
    long Id, string EmployeeNumber, string FullName, string? FullNameEn, string Category, string Status,
    long OrgUnitId, string? OrgUnitName, long? PositionId, string JobTitle, long? SiteId, string? SiteName,
    DateOnly? HireDate, long? UserId, string RowVersion);

/// <summary>Status is absent on purpose: it changes through personnel actions, not through an edit.</summary>
public sealed record SaveEmployeeRequest(
    string EmployeeNumber, string FullName, string? FullNameEn, string Category,
    long OrgUnitId, long? PositionId, string JobTitle, long? SiteId,
    DateOnly? HireDate, long? UserId, string? RowVersion = null);

public sealed record EmployeeListQuery(
    string? Search = null, long? OrgUnitId = null, long? SiteId = null,
    string? Category = null, string? Status = null, int Page = 1, int PageSize = 20);

public interface ISiteService
{
    Task<IReadOnlyList<SiteDto>> ListAsync(CancellationToken ct = default);
    Task<Result<SiteDto>> CreateAsync(SaveSiteRequest request, CancellationToken ct = default);
    Task<Result<SiteDto>> UpdateAsync(long id, SaveSiteRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<SiteLocationDto>> ListLocationsAsync(long siteId, CancellationToken ct = default);
    Task<Result<SiteLocationDto>> CreateLocationAsync(long siteId, SaveSiteLocationRequest request, CancellationToken ct = default);
    Task<Result<SiteLocationDto>> UpdateLocationAsync(long id, SaveSiteLocationRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteLocationAsync(long id, CancellationToken ct = default);
}

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> ListAsync(EmployeeListQuery query, CancellationToken ct = default);
    Task<Result<EmployeeDto>> GetAsync(long id, CancellationToken ct = default);
    Task<Result<EmployeeDto>> CreateAsync(SaveEmployeeRequest request, CancellationToken ct = default);
    Task<Result<EmployeeDto>> UpdateAsync(long id, SaveEmployeeRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default);
}
