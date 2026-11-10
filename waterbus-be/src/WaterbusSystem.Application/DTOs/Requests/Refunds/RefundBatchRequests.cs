namespace WaterbusSystem.Application.DTOs.Requests.Refunds;

/// <summary>Xem trước các Booking của một chuyến bị hủy/chấm dứt cần hoàn tiền.</summary>
public record ListRefundCandidatesRequest(Guid TripId, int Page = 1, int PageSize = 50);

/// <summary>Tạo yêu cầu hoàn tiền cho mọi Booking đủ điều kiện của một chuyến.</summary>
public record BatchRequestRefundsRequest(
    Guid TripId,
    string? Reason = null,
    string? IpAddress = null,
    string? CreatedBy = null);
