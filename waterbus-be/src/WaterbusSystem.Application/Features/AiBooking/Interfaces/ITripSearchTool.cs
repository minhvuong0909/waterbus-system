using WaterbusSystem.Application.Features.AiBooking.Models;

namespace WaterbusSystem.Application.Features.AiBooking.Interfaces;

/// <summary>
/// Tool tìm kiếm chuyến tàu dành cho AI Booking Assistant.
/// Ranh giới đảm bảo AI chỉ truy vấn qua query/service hợp lệ, không truy vấn database tùy tiện.
/// </summary>
public interface ITripSearchTool
{
    Task<TripSearchResult> SearchTripsAsync(
        TripSearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
