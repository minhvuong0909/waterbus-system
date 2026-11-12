using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Refunds;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IRefundExportService
{
    /// <summary>Dựng CSV các refund Manual đang chờ hoàn và ghi dấu "đã xuất". Không có dòng nào thì trả file chỉ có tiêu đề, không ghi dấu.</summary>
    Task<RefundExportFile> ExportAsync(ExportRefundsRequest request, CancellationToken ct = default);

    /// <summary>Chỉ đọc, phân trang: các refund Manual đang chờ nhân viên hoàn (cùng tiêu chí với CSV, không ghi dấu "đã xuất").</summary>
    Task<PaginatedList<PendingManualRefund>> ListPendingAsync(ListPendingRefundsRequest request, CancellationToken ct = default);
}
