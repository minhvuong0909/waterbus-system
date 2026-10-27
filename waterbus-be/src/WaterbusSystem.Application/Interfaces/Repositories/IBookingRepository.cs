using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IBookingRepository
{
    void Add(Booking booking);
    Task<List<SeatReservation>> GetPendingReservationsByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
    void AddSeatReservation(SeatReservation reservation);
}