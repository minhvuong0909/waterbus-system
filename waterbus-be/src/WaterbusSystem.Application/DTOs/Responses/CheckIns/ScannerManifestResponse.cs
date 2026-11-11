namespace WaterbusSystem.Application.DTOs.Responses.CheckIns;

public record ManifestTicketResponse(
    Guid TicketId,
    string TicketCode,
    string PassengerName,
    string SeatCode,
    string SeatClass,
    string BoardingStatus,
    decimal PaidFare);

/// <summary>
/// PublicBookingId là khoá tra cứu mà Staff Scanner dùng ngay khi quét QR (BR-QR-02) - bắt buộc
/// phải có trong manifest, không chỉ BookingCode (mã hiển thị, không dùng để tra cứu).
/// </summary>
public record ManifestBookingResponse(
    Guid BookingId,
    string PublicBookingId,
    string BookingCode,
    Guid TripId,
    string CustomerName,
    string? CustomerPhone,
    decimal QuotedTotal,
    int QrCredentialVersion,
    string Status,
    IReadOnlyList<ManifestTicketResponse> Tickets);

public record ManifestTripResponse(
    Guid TripId,
    string RouteName,
    string BoatName,
    string DepartureStationName,
    string ArrivalStationName,
    DateTimeOffset DepartureTime,
    DateTimeOffset? ArrivalTime,
    DateTimeOffset? SalesCloseAt,
    string TripType,
    string Status);

public record ScannerManifestResponse(
    Guid TripStopCallId,
    string StationName,
    DateTimeOffset? CheckInOpenAt,
    DateTimeOffset? CheckInCloseAt,
    ManifestTripResponse Trip,
    IReadOnlyList<ManifestBookingResponse> Bookings,
    DateTimeOffset GeneratedAt);
