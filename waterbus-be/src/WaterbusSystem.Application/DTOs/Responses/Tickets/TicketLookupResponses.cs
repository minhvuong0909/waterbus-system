using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Application.DTOs.Responses.Tickets;

/// <summary>
/// Phản hồi CỐ ĐỊNH cho yêu cầu xin OTP, dù thông tin có khớp hay không (không lộ mã đặt chỗ nào tồn tại).
/// </summary>
public record TicketLookupOtpResponse(int ExpiresInSeconds, int ResendAfterSeconds);

/// <summary>Vé của một hành khách. Không có giá, thanh toán, email hay số điện thoại.</summary>
public record TicketLookupTicketDto(string TicketCode, string PassengerName, string SeatCode, string SeatClass,
    string TicketStatus, string BoardingStatus, bool ServiceEntitled);

/// <summary>Một Booking (một chặng) của đơn, kèm QR lên tàu khi Booking đã thanh toán và có vé.</summary>
public record TicketLookupBookingDto(string BookingCode, string RouteName, DateTimeOffset DepartureTime,
    DateTimeOffset ArrivalTime, string TripStatus, string BoardingStation, string DisembarkingStation,
    string BookingStatus, IReadOnlyList<TicketLookupTicketDto> Tickets, BookingQrCredential? Qr);

public record TicketLookupResponse(IReadOnlyList<TicketLookupBookingDto> Bookings);
