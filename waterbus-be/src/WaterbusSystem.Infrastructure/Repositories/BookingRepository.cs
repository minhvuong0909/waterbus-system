using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class BookingRepository(ApplicationDbContext context) : IBookingRepository
{
    public void Add(Booking booking)
    {
        context.Bookings.Add(booking);
    }

    public Task<List<SeatReservation>> GetPendingReservationsByOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return context.SeatReservations
            .AsNoTracking()
            .Where(r =>
                r.Booking!.OrderId == orderId &&
                r.Status == ReservationStatus.Pending)
            .ToListAsync(cancellationToken);
    }

    public void AddSeatReservation(SeatReservation reservation)
    {
        context.SeatReservations.Add(reservation);
    }
    public Task<List<Booking>> GetForPaymentConfirmationAsync(Guid orderId, CancellationToken ct) =>
        context.Bookings.Include(b => b.Trip).Include(b => b.SeatReservations.Where(r => r.Status == ReservationStatus.Pending))
            .Where(b => b.OrderId == orderId).ToListAsync(ct);

    public Task<List<SeatReservation>> GetReservationsByStatusAsync(Guid orderId, ReservationStatus status, CancellationToken ct) =>
        context.SeatReservations.AsNoTracking().Where(r => r.Booking!.OrderId == orderId && r.Status == status).ToListAsync(ct);

    public void AddTicket(Ticket ticket) => context.Tickets.Add(ticket);

}
