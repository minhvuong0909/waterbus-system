namespace WaterbusSystem.Application.DTOs.Requests.TripPlanning;

/// <summary>
/// Sinh Trip từ Schedule cho các ngày [FromDate, ToDate] (tối đa 31 ngày). Ngày là ngày giờ Việt Nam. Hai mốc phút (tuỳ chọn) để
/// Admin cấu hình; để trống thì SalesCloseAt = giờ khởi hành (trần BR-TIME-01/02: không bán sau khi Trip rời bến), còn mốc CheckIn
/// không được đặt và Admin cấu hình sau (BR-CFG-01).
/// </summary>
public record GenerateTripsRequest(
    Guid ScheduleId, DateOnly FromDate, DateOnly ToDate, int? SalesCloseMinutesBeforeDeparture, int? CheckInOpenMinutesBeforeDeparture);

public record AssignTripBoatRequest(Guid BoatId);

/// <summary>
/// Trip ad-hoc ngoài Schedule (BR-SCH-10, ScheduleId để trống). Bắt buộc Route, loại dịch vụ, các bến thực sự phục vụ (theo thứ tự ghé,
/// VisitOrder = vị trí trong danh sách, cho phép lặp RouteStop với hành trình vòng) và giờ khởi hành. Tàu gán SAU qua
/// PUT /admin/trips/{id}/boat (BR-SCH-04: phải có tàu trước khi mở bán; BR-SCH-08 kiểm tra trùng lịch ở bước gán).
/// </summary>
public record CreateAdHocTripRequest(
    Guid RouteId, string TripType, DateTimeOffset DepartureTime, List<AdHocTripStopRequest> Stops,
    int? SalesCloseMinutesBeforeDeparture, int? CheckInOpenMinutesBeforeDeparture);

/// <summary>Mốc phút tính từ giờ khởi hành. Bến đầu: chỉ DepartureOffsetMin; bến cuối: chỉ ArrivalOffsetMin; bến giữa: cả hai.</summary>
public record AdHocTripStopRequest(Guid RouteStopId, int? ArrivalOffsetMin, int? DepartureOffsetMin);
