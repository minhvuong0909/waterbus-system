namespace WaterbusSystem.Application.Features.AiBooking.Models;

/// <summary>
/// Tiêu chí tìm kiếm chuyến tàu được chuẩn hóa từ câu hỏi của khách hàng.
/// </summary>
public record TripSearchCriteria
{
    public string? DepartureStationName { get; init; }
    public string? ArrivalStationName { get; init; }
    public Guid? DepartureStationId { get; init; }
    public Guid? ArrivalStationId { get; init; }
    /// <summary>Khách đã nói rõ bến duy nhất là bến đi hoặc bến đến; không tìm chiều ngược lại.</summary>
    public bool RestrictSingleStationToRequestedDirection { get; init; }
    public DateOnly? DepartureDate { get; init; }
    public TimeOnly? EarliestDepartureTime { get; init; }
    public TimeOnly? LatestDepartureTime { get; init; }
    public TimeOnly? PreferredDepartureTime { get; init; }
    public TimeOnly? LatestArrivalTime { get; init; }
    public string? TripType { get; init; }
    public int? PassengerCount { get; init; }
    public int? MinimumAvailableSeats { get; init; }
    public decimal? PriceGreaterThan { get; init; }
    public decimal? PriceLessThan { get; init; }
}
