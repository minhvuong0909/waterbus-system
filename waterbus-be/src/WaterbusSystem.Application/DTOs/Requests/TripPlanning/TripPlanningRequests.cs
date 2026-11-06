namespace WaterbusSystem.Application.DTOs.Requests.TripPlanning;

/// <summary>
/// Sinh Trip từ Schedule cho các ngày [FromDate, ToDate] (tối đa 31 ngày). Ngày là ngày giờ Việt Nam. Hai mốc phút (tuỳ chọn) để
/// Admin cấu hình; để trống thì SalesCloseAt/CheckIn... không được đặt và Admin cấu hình sau (BR-TIME-02, BR-CFG-01).
/// </summary>
public record GenerateTripsRequest(
    Guid ScheduleId, DateOnly FromDate, DateOnly ToDate, int? SalesCloseMinutesBeforeDeparture, int? CheckInOpenMinutesBeforeDeparture);

public record AssignTripBoatRequest(Guid BoatId);
