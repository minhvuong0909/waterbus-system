using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.DTOs.Responses.Accounts;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class AccountAccessRepository(ApplicationDbContext context) : IAccountAccessRepository
{
    public Task<PaginatedList<MyOrderSummaryDto>> GetOrdersPageAsync(Guid accountId, GetMyOrdersRequest request, CancellationToken ct) =>
        context.PurchaseOrders.AsNoTracking()
            .Where(o => !o.IsDeleted && o.PassengerAccountId == accountId && o.Status != OrderStatus.Draft)
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Select(o => new MyOrderSummaryDto(o.Id, o.Status.ToString(), o.PurchaseMode.ToString(), o.QuotedTotal, o.Currency,
                o.CreatedAt, o.ConfirmedAt,
                o.Bookings.Where(b => !b.IsDeleted).OrderBy(b => b.Trip!.DepartureTime)
                    .Select(b => new MyOrderBookingDto(b.Id, b.BookingCode, b.TripId, b.Trip!.Route!.Name, b.Trip.DepartureTime,
                        b.Status.ToString(), b.PaymentStatus.ToString(), b.Tickets.Count)).ToList()))
            .ToPaginatedListAsync(request.PageNumber, request.PageSize, ct);

    public Task<TicketDetailDto?> FindTicketAsync(Guid orderId, Guid ticketId, CancellationToken ct) =>
        Tickets().Where(t => t.Id == ticketId && t.Booking!.OrderId == orderId).Select(ToDetail).FirstOrDefaultAsync(ct);

    public Task<TicketDetailDto?> FindTicketForAccountAsync(Guid accountId, Guid ticketId, CancellationToken ct) =>
        Tickets().Where(t => t.Id == ticketId && t.Booking!.Order!.PassengerAccountId == accountId)
            .Select(ToDetail).FirstOrDefaultAsync(ct);

    public Task<TripAccessDto?> FindTripAccessAsync(Guid bookingId, CancellationToken ct) =>
        context.Bookings.AsNoTracking().Where(b => b.Id == bookingId && !b.IsDeleted)
            .Select(b => new TripAccessDto(b.BookingCode, b.TripId, b.Trip!.Route!.Name, b.Trip.DepartureTime, b.Trip.ArrivalTime,
                b.Trip.Status.ToString(), b.BoardingCall!.RouteStop!.Station!.Name, b.DisembarkingCall!.RouteStop!.Station!.Name,
                b.Status.ToString(),
                b.Tickets.Where(t => !t.IsDeleted).OrderBy(t => t.SeatReservation!.SeatCodeAtSale)
                    .Select(t => new TripAccessTicketDto(t.SeatReservation!.PassengerName, t.SeatReservation.SeatCodeAtSale ?? "",
                        t.Status.ToString(), t.BoardingStatus.ToString())).ToList()))
            .FirstOrDefaultAsync(ct);

    public async Task<BookingStatus?> GetBookingStatusAsync(Guid orderId, Guid bookingId, CancellationToken ct) =>
        await context.Bookings.AsNoTracking().Where(b => b.Id == bookingId && b.OrderId == orderId && !b.IsDeleted)
            .Select(b => (BookingStatus?)b.Status).FirstOrDefaultAsync(ct);

    private IQueryable<Ticket> Tickets() => context.Tickets.AsNoTracking().Where(t => !t.IsDeleted);

    private static readonly Expression<Func<Ticket, TicketDetailDto>> ToDetail = t => new TicketDetailDto(
        t.Id, t.TicketCode, t.Booking!.OrderId, t.BookingId, t.Booking.BookingCode, t.Booking.PublicBookingId,
        t.Booking.TripId, t.Booking.Trip!.Route!.Name, t.Booking.Trip.DepartureTime, t.Booking.Trip.ArrivalTime,
        t.Booking.Trip.Status.ToString(), t.Booking.BoardingCall!.RouteStop!.Station!.Name,
        t.Booking.DisembarkingCall!.RouteStop!.Station!.Name, t.SeatReservation!.PassengerName,
        t.SeatReservation.SeatCodeAtSale ?? "", t.SeatReservation.SeatClassAtSale ?? "", t.Booking.Order!.Status.ToString(),
        t.Booking.Status.ToString(), t.Booking.PaymentStatus.ToString(), t.Status.ToString(), t.BoardingStatus.ToString(),
        t.Status == TicketStatus.Valid && t.Booking.Status == BookingStatus.Confirmed,
        t.PaidFareSnapshot, t.IssuedAt);
}
