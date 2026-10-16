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
    public DateOnly? DepartureDate { get; init; }
    public string? TripType { get; init; }
    public int? PassengerCount { get; init; }
}
