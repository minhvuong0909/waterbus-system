using Microsoft.Extensions.Logging;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Refunds;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Refunds.Services;

/// <summary>
/// Xuất danh sách refund Manual đang chờ nhân viên hoàn tiền ra CSV (BR-RF-10). Chỉ đọc dữ liệu refund rồi ghi dấu "đã xuất"
/// (thời điểm + Admin) để biết việc đã được giao; không đổi trạng thái tài chính. Cho phép xuất lại. Log chỉ ghi số lượng, không ghi dữ liệu cá nhân.
/// </summary>
public sealed class RefundExportService(
    IRefundExportRepository repository, ICurrentUserService currentUser, IDateTimeService clock,
    ILogger<RefundExportService> logger) : IRefundExportService
{
    /// <summary>Trần số dòng một lần xuất; vượt thì yêu cầu lọc theo chuyến (tránh file quá lớn và truy vấn nặng).</summary>
    public const int MaxRows = 5000;

    public async Task<RefundExportFile> ExportAsync(ExportRefundsRequest request, CancellationToken ct = default)
    {
        if (!Guid.TryParse(currentUser.UserId, out var adminId))
            throw new ForbiddenAccessException("Không xác định được người quản trị đang xuất danh sách hoàn tiền.");

        var rows = await repository.ListPendingManualAsync(request.TripId, 0, MaxRows + 1, ct);
        if (rows.Count > MaxRows)
            throw new ConflictException($"Có hơn {MaxRows} khoản đang chờ hoàn. Hãy lọc theo chuyến (tripId) để xuất từng phần.");

        var now = clock.Now;
        var content = RefundCsvWriter.Write(rows);
        if (rows.Count > 0) await repository.MarkExportedAsync(rows.Select(r => r.RefundId).ToList(), adminId, now, ct);

        logger.LogInformation("Admin {AdminId} đã xuất {Count} refund Manual ra CSV (tripId={TripId}).", adminId, rows.Count, request.TripId);
        var fileName = $"hoan-tien-{now.ToOffset(TimeSpan.FromHours(7)):yyyyMMdd-HHmm}.csv";
        return new RefundExportFile(fileName, content, rows.Count);
    }

    public async Task<PaginatedList<PendingManualRefund>> ListPendingAsync(ListPendingRefundsRequest request, CancellationToken ct = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var total = await repository.CountPendingManualAsync(request.TripId, ct);
        var items = await repository.ListPendingManualAsync(request.TripId, (page - 1) * pageSize, pageSize, ct);
        return new PaginatedList<PendingManualRefund>(items.ToList(), total, page, pageSize);
    }
}
