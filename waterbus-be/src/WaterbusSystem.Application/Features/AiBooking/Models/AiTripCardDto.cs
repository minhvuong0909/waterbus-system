namespace WaterbusSystem.Application.Features.AiBooking.Models;

/// <summary>
/// DTO thông tin chuyến tàu có cấu trúc (Trip Card) trả về cho Frontend hiển thị.
/// Tách biệt hoàn toàn khỏi văn bản tự nhiên của model, ngăn ngừa việc AI bịa giá hoặc bến.
/// </summary>
/// <param name="TripId">Mã định danh duy nhất của chuyến tàu.</param>
/// <param name="RouteName">Tên tuyến buýt đường sông.</param>
/// <param name="DepartureStationName">Tên bến khởi hành.</param>
/// <param name="ArrivalStationName">Tên bến kết thúc.</param>
/// <param name="DepartureTime">Thời gian khởi hành.</param>
/// <param name="ArrivalTime">Thời gian cập bến dự kiến.</param>
/// <param name="DurationMinutes">Thời gian hành trình tính theo phút.</param>
/// <param name="TripType">Loại chuyến ("Commuter", "Sightseeing"), theo Domain.Enums.TripType.</param>
/// <param name="BasePrice">
/// Giá vé cơ bản. Nullable — nếu pricing engine/FareRule chưa được owner xác nhận,
/// giá trị này có thể là null để tránh khẳng định giá 0đ không đúng sự thật.
/// </param>
/// <param name="IsPriceVerified">
/// Đánh dấu giá đã được xác thực từ pricing engine thật hay chưa.
/// </param>
/// <param name="AvailableSeatsCount">Số lượng ghế còn trống trên chuyến.</param>
/// <param name="BookingUrl">
/// Đường dẫn đặt vé do tool hệ thống tạo ra (null nếu chưa đủ thông tin tạo link).
/// Tuyệt đối không để AI tự bịa URL.
/// </param>
/// <param name="Stops">Danh sách các bến dừng hợp lệ của chuyến.</param>
public record AiTripCardDto(
    Guid TripId,
    string RouteName,
    string DepartureStationName,
    string ArrivalStationName,
    DateTimeOffset DepartureTime,
    DateTimeOffset ArrivalTime,
    int DurationMinutes,
    string TripType,
    decimal? BasePrice,
    bool IsPriceVerified,
    int AvailableSeatsCount,
    string? BookingUrl,
    IReadOnlyList<AiTripStopDto> Stops);
