using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Trips.Queries.SearchTrips;

/// <summary>
/// Một bến dừng hợp lệ trên hành trình của chuyến tàu, phục vụ FE/Mobile cho khách chọn bến lên/bến xuống
/// </summary>
public record TripStopDto(Guid StationId, string StationName, int OrderIndex);

/// <summary>
/// DTO thông tin chuyến tàu phục vụ hiển thị Trip Card
/// </summary>
public record TripDto(
    Guid Id,
    string RouteName,
    string DepartureStationName,
    string ArrivalStationName,
    DateTimeOffset DepartureTime,
    DateTimeOffset ArrivalTime,
    int DurationMinutes,
    string TripType,
    decimal BasePrice,
    int AvailableSeatsCount,
    List<TripStopDto> Stops);

/// <summary>
/// Query tìm kiếm chuyến tàu theo điều kiện lọc
/// </summary>
public record SearchTripsQuery(
    Guid? DepartureStationId,
    Guid? ArrivalStationId,
    DateTime? DepartureDate,
    TripType? TripType) : IRequest<List<TripDto>>;

public class SearchTripsQueryHandler : IRequestHandler<SearchTripsQuery, List<TripDto>>
{
    private readonly IApplicationDbContext _context;

    public SearchTripsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TripDto>> Handle(SearchTripsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Trips
            .AsNoTracking()
            .Include(t => t.Route)
                .ThenInclude(r => r!.DepartureStation)
            .Include(t => t.Route)
                .ThenInclude(r => r!.ArrivalStation)
            .Include(t => t.Boat)
            .Include(t => t.Bookings).ThenInclude(b => b.SeatReservations)
            .Where(t => !t.IsDeleted && t.Status == TripStatus.Scheduled);

        if (request.DepartureStationId.HasValue)
        {
            query = query.Where(t => t.Route!.DepartureStationId == request.DepartureStationId.Value);
        }

        if (request.ArrivalStationId.HasValue)
        {
            query = query.Where(t => t.Route!.ArrivalStationId == request.ArrivalStationId.Value);
        }

        if (request.DepartureDate.HasValue)
        {
            var date = request.DepartureDate.Value.Date;
            query = query.Where(t => t.DepartureTime.Date == date);
        }

        if (request.TripType.HasValue)
        {
            query = query.Where(t => t.TripType == request.TripType.Value);
        }

        var trips = await query.OrderBy(t => t.DepartureTime).ToListAsync(cancellationToken);

        // Nạp toàn bộ bến đang hoạt động 1 lần duy nhất để suy ra danh sách bến dừng hợp lệ của từng chuyến.
        // GIẢ ĐỊNH: mọi Trip dừng tuần tự ở mọi bến có OrderIndex nằm giữa Departure và Arrival của Route
        // (xem ghi chú chi tiết tại CreateBookingCommandHandler).
        var allStations = await _context.Stations
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.IsActive)
            .Select(s => new TripStopDto(s.Id, s.Name, s.OrderIndex))
            .ToListAsync(cancellationToken);

        return trips.Select(t =>
        {
            var totalBoatSeats = t.Boat?.TotalSeats ?? 60;
            var bookedSeatsCount = t.Bookings.SelectMany(b => b.SeatReservations)
                .Where(r => r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
                .Select(r => r.SeatId).Distinct().Count();

            var availableSeats = Math.Max(0, totalBoatSeats - bookedSeatsCount);

            var depOrder = t.Route?.DepartureStation?.OrderIndex ?? 0;
            var arrOrder = t.Route?.ArrivalStation?.OrderIndex ?? 0;
            var minOrder = Math.Min(depOrder, arrOrder);
            var maxOrder = Math.Max(depOrder, arrOrder);
            var ascending = arrOrder >= depOrder;

            var stops = allStations
                .Where(s => s.OrderIndex >= minOrder && s.OrderIndex <= maxOrder)
                .OrderBy(s => ascending ? s.OrderIndex : -s.OrderIndex)
                .ToList();

            return new TripDto(
                t.Id,
                t.Route?.Name ?? "Tuyến Buýt Đường Sông",
                t.Route?.DepartureStation?.Name ?? "",
                t.Route?.ArrivalStation?.Name ?? "",
                t.DepartureTime,
                t.ArrivalTime,
                t.Route?.EstimatedDurationMinutes ?? 45,
                t.TripType.ToString(),
                0m /* TODO: implement FareRule pricing */,
                availableSeats,
                stops);
        }).ToList();
    }
}
