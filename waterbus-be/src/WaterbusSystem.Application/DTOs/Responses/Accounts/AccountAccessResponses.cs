namespace WaterbusSystem.Application.DTOs.Responses.Accounts;

/// <summary>Kết quả liên kết Order vào tài khoản Passenger (BR-ID-04). AlreadyLinked = gọi lặp, không đổi gì.</summary>
public record ClaimOrderResponse(Guid OrderId, bool AlreadyLinked);

/// <summary>Token mới của Manage Order secure link. Token gốc chỉ trả về một lần; DB chỉ lưu hash (BR-ID-02).</summary>
public record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

public record RevokeAccessResponse(int RevokedCount);

public record MyOrderBookingDto(Guid BookingId, string BookingCode, Guid TripId, string RouteName,
    DateTimeOffset DepartureTime, string BookingStatus, string PaymentStatus, int TicketCount);

/// <summary>Một dòng lịch sử đơn của Passenger. Trạng thái Order/Booking/Payment tách riêng, không gộp.</summary>
public record MyOrderSummaryDto(Guid OrderId, string OrderStatus, string PurchaseMode, decimal QuotedTotal,
    string Currency, DateTimeOffset CreatedAt, DateTimeOffset? ConfirmedAt, IReadOnlyList<MyOrderBookingDto> Bookings);

/// <summary>
/// Chi tiết vé dành cho Purchaser (Guest có token hoặc Passenger sở hữu). Tách rõ 4 trục trạng thái:
/// thanh toán (PaymentStatus), Booking, Ticket (entitlement) và lên tàu (BoardingStatus).
/// </summary>
public record TicketDetailDto(Guid TicketId, string TicketCode, Guid OrderId, Guid BookingId, string BookingCode,
    string PublicBookingId, Guid TripId, string RouteName, DateTimeOffset DepartureTime, DateTimeOffset ArrivalTime,
    string TripStatus, string BoardingStation, string DisembarkingStation, string PassengerName, string SeatCode,
    string SeatClass, string OrderStatus, string BookingStatus, string PaymentStatus, string TicketStatus,
    string BoardingStatus, bool ServiceEntitled, decimal PaidFare, DateTimeOffset? IssuedAt);

public record TripAccessTicketDto(string PassengerName, string SeatCode, string TicketStatus, string BoardingStatus);

/// <summary>
/// Thông tin đi tàu qua Passenger TripAccess Link (BR-ID-05): KHÔNG có giá, thanh toán, thông tin Purchaser hay QR.
/// </summary>
public record TripAccessDto(string BookingCode, Guid TripId, string RouteName, DateTimeOffset DepartureTime,
    DateTimeOffset ArrivalTime, string TripStatus, string BoardingStation, string DisembarkingStation,
    string BookingStatus, IReadOnlyList<TripAccessTicketDto> Tickets);
