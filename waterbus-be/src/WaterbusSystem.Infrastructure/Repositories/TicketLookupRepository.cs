using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.DTOs.Responses.Tickets;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

/// <summary>Truy vấn chỉ đọc cho tra cứu vé (AsNoTracking, bỏ bản ghi xoá mềm).</summary>
public sealed class TicketLookupRepository(ApplicationDbContext context) : ITicketLookupRepository
{
    public Task<TicketLookupMatch?> FindMatchAsync(string bookingCode, string email, CancellationToken ct) =>
        context.Bookings.AsNoTracking()
            .Where(b => !b.IsDeleted && b.BookingCode == bookingCode && !b.Order!.IsDeleted && b.Order.PurchaserEmail == email)
            .Select(b => new TicketLookupMatch(b.OrderId, b.Order!.PurchaserName))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TicketLookupBookingData>> GetBookingsAsync(Guid orderId, CancellationToken ct) =>
        await context.Bookings.AsNoTracking()
            .Where(b => !b.IsDeleted && b.OrderId == orderId)
            .OrderBy(b => b.Trip!.DepartureTime)
            .Select(b => new TicketLookupBookingData(
                b.BookingCode, b.PublicBookingId, b.TripId, b.QrCredentialVersion,
                b.QrIssuedAt, b.ConfirmedAt, b.Tickets.Where(t => !t.IsDeleted).Min(t => t.IssuedAt), b.Status,
                b.Trip!.Route!.Name, b.Trip.DepartureTime, b.Trip.ArrivalTime, b.Trip.Status.ToString(),
                b.BoardingCall!.RouteStop!.Station!.Name, b.DisembarkingCall!.RouteStop!.Station!.Name,
                b.Tickets.Where(t => !t.IsDeleted).OrderBy(t => t.SeatReservation!.SeatCodeAtSale)
                    .Select(t => new TicketLookupTicketDto(
                        t.TicketCode, t.SeatReservation!.PassengerName, t.SeatReservation.SeatCodeAtSale ?? "",
                        t.SeatReservation.SeatClassAtSale ?? "", t.Status.ToString(), t.BoardingStatus.ToString(),
                        t.Status == TicketStatus.Valid && b.Status == BookingStatus.Confirmed)).ToList()))
            .ToListAsync(ct);
}
