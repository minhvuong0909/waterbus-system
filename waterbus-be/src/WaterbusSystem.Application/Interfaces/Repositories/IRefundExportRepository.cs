using WaterbusSystem.Application.DTOs.Responses.Refunds;

namespace WaterbusSystem.Application.Interfaces.Repositories;

/// <summary>Truy vấn và ghi dấu cho việc xuất CSV các refund Manual đang chờ nhân viên hoàn.</summary>
public interface IRefundExportRepository
{
    /// <summary>
    /// Refund Manual ở trạng thái Requested/Processing (cũ nhất trước), bỏ qua <paramref name="skip"/> và lấy tối đa <paramref name="take"/> dòng.
    /// Đã Succeeded/Failed không có. <paramref name="tripId"/> null = mọi chuyến.
    /// </summary>
    Task<IReadOnlyList<PendingManualRefund>> ListPendingManualAsync(Guid? tripId, int skip, int take, CancellationToken ct);

    Task<int> CountPendingManualAsync(Guid? tripId, CancellationToken ct);

    /// <summary>Ghi thời điểm và Admin xuất lần gần nhất (set-based, không đụng RowVersion để không cản xác nhận đang diễn ra).</summary>
    Task MarkExportedAsync(IReadOnlyCollection<Guid> refundIds, Guid adminId, DateTimeOffset exportedAt, CancellationToken ct);
}
