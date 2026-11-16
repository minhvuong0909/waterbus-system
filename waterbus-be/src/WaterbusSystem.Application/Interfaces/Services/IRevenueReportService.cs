using WaterbusSystem.Application.DTOs.Requests.Reports;
using WaterbusSystem.Application.DTOs.Responses.Reports;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IRevenueReportService
{
    Task<RevenueReportResponse> GetAsync(GetRevenueReportRequest request, CancellationToken cancellationToken = default);
}
