namespace WaterbusSystem.Application.Features.AiBooking.Models;

/// <summary>
/// Bến dừng hợp lệ trên hành trình chuyến tàu trong Trip Card
/// </summary>
public record AiTripStopDto(
    Guid StationId,
    string StationName,
    int OrderIndex);
