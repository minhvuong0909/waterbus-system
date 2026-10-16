namespace WaterbusSystem.Application.Features.AiBooking.Models;

/// <summary>
/// Tiêu chí để tool tạo link đặt vé từ chuyến tàu đã chọn.
/// </summary>
public record BookingLinkCriteria
{
    public Guid TripId { get; init; }
    public Guid? BoardingStationId { get; init; }
    public Guid? DisembarkingStationId { get; init; }
    public int? PassengerCount { get; init; }
}
