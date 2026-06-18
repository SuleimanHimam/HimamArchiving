using Archiving.Application.Common.Models;
using Archiving.Application.Features.IncomingMail;

namespace Archiving.Application.Common.Interfaces;

public interface IIncomingMailService
{
    Task<PagedResult<IncomingMailListItem>> ListAsync(IncomingMailQuery query, CancellationToken ct = default);
    Task<Result<IncomingMailDetail>> GetAsync(long id, CancellationToken ct = default);
    Task<Result<IncomingMailDetail>> CreateAsync(CreateIncomingMailRequest request, CancellationToken ct = default);
    Task<Result<IncomingMailDetail>> ActAsync(long id, IncomingMailActionRequest request, CancellationToken ct = default);
}
