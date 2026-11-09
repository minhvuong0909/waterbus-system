using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface ITripPlanningRepository
{
    /// <summary>Schedule (chỉ đọc) kèm Route và các ScheduleStop chưa xoá (kèm RouteStop), hoặc NotFound.</summary>
    Task<Schedule> GetScheduleForGenerationAsync(Guid scheduleId, CancellationToken cancellationToken = default);

    /// <summary>Giờ khởi hành đã có Trip của lịch trong khoảng, KỂ CẢ Trip xoá mềm (unique (ScheduleId, DepartureTime) tính cả chúng).</summary>
    Task<HashSet<DateTimeOffset>> ListExistingDeparturesAsync(Guid scheduleId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    /// <summary>Route (chỉ đọc, chưa xoá) kèm RouteStop chưa xoá, hoặc NotFound.</summary>
    Task<Route> GetRouteWithStopsAsync(Guid routeId, CancellationToken cancellationToken = default);

    void AddTrip(Trip trip);

    /// <summary>Trip (có theo dõi, chưa xoá) hoặc NotFound.</summary>
    Task<Trip> GetTripTrackedAsync(Guid tripId, CancellationToken cancellationToken = default);

    /// <summary>Tàu (chỉ đọc, chưa xoá) kèm ghế đang hoạt động, hoặc NotFound.</summary>
    Task<Boat> GetBoatWithActiveSeatsAsync(Guid boatId, CancellationToken cancellationToken = default);

    /// <summary>Tàu đã có Trip khác (chưa Cancelled/Terminated) chồng thời gian [from, to).</summary>
    Task<bool> HasOverlappingTripAsync(Guid boatId, DateTimeOffset from, DateTimeOffset to, Guid excludeTripId, CancellationToken cancellationToken = default);

    /// <summary>SeatReservation Pending/Confirmed của Trip (có theo dõi) kèm Seat hiện tại.</summary>
    Task<List<SeatReservation>> ListLiveReservationsTrackedAsync(Guid tripId, CancellationToken cancellationToken = default);
}
