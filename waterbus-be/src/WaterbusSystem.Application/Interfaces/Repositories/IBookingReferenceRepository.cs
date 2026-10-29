using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IBookingReferenceRepository
{
    Task<Trip?> GetTripWithStopsAsync(
        Guid tripId,
        CancellationToken ct = default);

    Task<List<Seat>> GetActiveSeatsAsync(
        IReadOnlyCollection<Guid> seatIds,
        CancellationToken ct = default);

    Task<List<FareRule>> GetEffectiveFaresAsync(
        TripType tripType,
        DateTimeOffset now,
        CancellationToken ct = default);
}
