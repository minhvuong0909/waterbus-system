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
}