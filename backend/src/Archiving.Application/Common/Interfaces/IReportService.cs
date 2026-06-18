using Archiving.Application.Features.Reports;

namespace Archiving.Application.Common.Interfaces;

public interface IReportService
{
    Task<DashboardSummary> GetDashboardAsync(CancellationToken ct = default);
}
