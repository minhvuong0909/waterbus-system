using WaterbusSystem.Application.DTOs.Responses.Tickets;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Interfaces.Repositories;

/// <summary>Đơn khớp với (mã đặt chỗ, email người mua).</summary>
public sealed record TicketLookupMatch(Guid OrderId, string PurchaserName);

/// <summary>Dữ liệu một Booking để dựng kết quả tra cứu; các trường QR chỉ dùng để ký, không trả ra ngoài.</summary>
public sealed record TicketLookupBookingData(
    string BookingCode, string PublicBookingId, Guid TripId, int QrCredentialVersion,
    DateTimeOffset? QrIssuedAt, DateTimeOffset? ConfirmedAt, DateTimeOffset? FirstTicketIssuedAt, BookingStatus Status,
    string RouteName, DateTimeOffset DepartureTime, DateTimeOffset ArrivalTime, string TripStatus,
    string BoardingStation, string DisembarkingStation, IReadOnlyList<TicketLookupTicketDto> Tickets);

/// <summary>Truy vấn chỉ đọc cho tra cứu vé của người mua (khách không cần tài khoản).</summary>
public interface ITicketLookupRepository
{
    /// <summary>
    /// Tìm đơn theo BookingCode và PurchaserEmail (không phân biệt hoa thường). Bỏ qua bản ghi đã xoá mềm.
    /// Caller đã chuẩn hoá: email thường, BookingCode chữ hoa.
    /// </summary>
    Task<TicketLookupMatch?> FindMatchAsync(string bookingCode, string email, CancellationToken ct);

    /// <summary>Mọi Booking (kể cả chặng về của đơn khứ hồi) và vé của đơn.</summary>
    Task<IReadOnlyList<TicketLookupBookingData>> GetBookingsAsync(Guid orderId, CancellationToken ct);
}
