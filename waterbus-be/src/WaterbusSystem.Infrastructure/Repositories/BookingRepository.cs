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

    public async Task<Booking> GetForRefundAsync(Guid bookingId, CancellationToken ct) =>
        await context.Bookings.Include(b => b.Trip).ThenInclude(t => t!.TripStopCalls)
            .Include(b => b.Tickets).Include(b => b.SeatReservations).Include(b => b.Order)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct)
        ?? throw new WaterbusSystem.Domain.Exceptions.NotFoundException(nameof(Booking), bookingId);

    public Task<bool> HasTicketsForOrderAsync(Guid orderId, CancellationToken ct) =>
        context.Tickets.AnyAsync(t => t.Booking!.OrderId == orderId, ct);

}
