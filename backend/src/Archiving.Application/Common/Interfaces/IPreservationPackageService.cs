using Archiving.Application.Common.Models;
using Archiving.Application.Features.Preservation;

namespace Archiving.Application.Common.Interfaces;

/// <summary>OAIS information packages (ISO 14721): SIP/AIP/DIP + Representation Information.</summary>
public interface IPreservationPackageService
{
    /// <summary>Ensures the document's SIP/AIP (and representation info) are current, then returns them.</summary>
    Task<Result<DocumentPackagesDto>> GetPackagesAsync(long documentId, CancellationToken ct = default);

    /// <summary>Builds the AIP as a ZIP (preservation files + manifest.json) and records a DIP.</summary>
    Task<Result<AipExport>> ExportAipAsync(long documentId, CancellationToken ct = default);
}

/// <summary>The archive's Designated Community record (ISO 14721).</summary>
public interface IDesignatedCommunityService
{
    Task<DesignatedCommunityDto> GetAsync(CancellationToken ct = default);
    Task<DesignatedCommunityDto> UpdateAsync(DesignatedCommunityDto request, CancellationToken ct = default);
}
