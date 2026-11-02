using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Requests.Trips;

/// <summary>
/// Chuyến cho màn hình vận hành (Admin: mọi chuyến; thuyền trưởng: chỉ chuyến của tàu mình), theo giờ khởi hành tăng dần.
/// Mặc định từ 12 giờ trước tới 7 ngày sau; tối đa 31 ngày và 200 chuyến mỗi lần.
/// </summary>
public record GetTripOperationsRequest(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    IReadOnlyList<TripStatus>? Statuses = null);
