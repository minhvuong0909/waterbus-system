using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IBookingRepository
{
    void Add(Booking booking);
    Task<List<SeatReservation>> GetPendingReservationsByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
    void AddSeatReservation(SeatReservation reservation);
    Task<List<Booking>> GetForPaymentConfirmationAsync(Guid orderId, CancellationToken ct);
    Task<List<SeatReservation>> GetReservationsByStatusAsync(Guid orderId, WaterbusSystem.Domain.Enums.ReservationStatus status, CancellationToken ct);
    void AddTicket(Ticket ticket);

}
