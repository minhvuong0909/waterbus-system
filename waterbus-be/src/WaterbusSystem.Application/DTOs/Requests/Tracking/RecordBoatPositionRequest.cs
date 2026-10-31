namespace WaterbusSystem.Application.DTOs.Requests.Tracking;

/// <summary>
/// Ghi nhận 1 vị trí của Boat (BR-TRK-01: GPS của tàu, hoặc giả lập theo Route khi demo).
/// Nguồn GPS bắt buộc có ReportedByCaptainId và phải là Captain được gán cho Boat.
/// </summary>
public record RecordBoatPositionRequest(
    Guid BoatId,
    decimal Latitude,
    decimal Longitude,
    decimal? SpeedKnots,
    DateTimeOffset ObservedAt,
    string Source,
    Guid? ReportedByCaptainId);
