using Archiving.Application.Common.Models;
using Archiving.Application.Features.OutgoingMail;

namespace Archiving.Application.Common.Interfaces;

public interface IOutgoingMailService
{
    Task<PagedResult<OutgoingMailListItem>> ListAsync(OutgoingMailQuery query, CancellationToken ct = default);
    Task<Result<OutgoingMailDetail>> GetAsync(long id, CancellationToken ct = default);
    Task<Result<OutgoingMailDetail>> CreateAsync(CreateOutgoingMailRequest request, CancellationToken ct = default);
    Task<Result<OutgoingMailDetail>> UpdateAsync(long id, UpdateOutgoingMailRequest request, CancellationToken ct = default);
    Task<Result<OutgoingMailDetail>> ActAsync(long id, OutgoingMailActionRequest request, CancellationToken ct = default);
}
