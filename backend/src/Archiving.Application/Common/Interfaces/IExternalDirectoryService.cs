using Archiving.Application.Common.Models;
using Archiving.Application.Features.External;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Directory of external organizations and their contacts (EXT, v1.1) — usable as
/// correspondence senders/recipients.</summary>
public interface IExternalDirectoryService
{
    // Organizations
    Task<PagedResult<ExternalOrgDto>> ListOrgsAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<Result<ExternalOrgDto>> GetOrgAsync(long id, CancellationToken ct = default);
    Task<Result<ExternalOrgDto>> CreateOrgAsync(CreateExternalOrgDto dto, CancellationToken ct = default);
    Task<Result<ExternalOrgDto>> UpdateOrgAsync(long id, CreateExternalOrgDto dto, CancellationToken ct = default);
    Task<Result<bool>> DeleteOrgAsync(long id, CancellationToken ct = default);

    // Contacts
    Task<PagedResult<ExternalContactDto>> ListContactsAsync(long? organizationId, string? search, int page, int pageSize, CancellationToken ct = default);
    Task<Result<ExternalContactDto>> CreateContactAsync(CreateExternalContactDto dto, CancellationToken ct = default);
    Task<Result<ExternalContactDto>> UpdateContactAsync(long id, CreateExternalContactDto dto, CancellationToken ct = default);
    Task<Result<bool>> DeleteContactAsync(long id, CancellationToken ct = default);
}
