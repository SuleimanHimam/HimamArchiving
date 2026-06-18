using Archiving.Application.Common.Models;
using Archiving.Application.Features.Metadata;

namespace Archiving.Application.Common.Interfaces;

/// <summary>ISO 23081 records metadata: agents, relationships and business activities of a record.</summary>
public interface IRecordMetadataService
{
    /// <summary>Ensures a document's derivable metadata (creator/owner/unit agents, version relationships)
    /// is present, then returns the full metadata graph including business activities (workflows).</summary>
    Task<Result<RecordMetadataDto>> GetForDocumentAsync(long documentId, CancellationToken ct = default);

    Task<Result<RecordAgentDto>> AddAgentAsync(string recordType, long recordId, AddAgentRequest request, CancellationToken ct = default);
    Task<Result<RecordRelationshipDto>> AddRelationshipAsync(AddRelationshipRequest request, CancellationToken ct = default);
}
