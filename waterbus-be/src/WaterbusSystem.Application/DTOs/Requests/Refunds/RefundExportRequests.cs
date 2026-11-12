namespace WaterbusSystem.Application.DTOs.Requests.Refunds;

/// <summary>Xuất CSV các refund Manual đang chờ nhân viên hoàn tiền; TripId để giới hạn theo một chuyến (bỏ trống = tất cả).</summary>
public record ExportRefundsRequest(Guid? TripId = null);

/// <summary>Xác nhận đã hoàn nhiều khoản một lần; mỗi dòng được xử lý độc lập như <c>confirm-manual</c>.</summary>
public record BatchConfirmManualRefundsRequest(IReadOnlyList<ConfirmManualRefundRequest> Items);

/// <summary>Danh sách (chỉ đọc, phân trang) các refund Manual đang chờ hoàn; không ghi dấu "đã xuất".</summary>
public record ListPendingRefundsRequest(Guid? TripId = null, int Page = 1, int PageSize = 50);
