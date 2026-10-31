using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
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
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTime;

    public SearchTripsQueryHandler(IApplicationDbContext context, IDateTimeService dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<List<TripDto>> Handle(SearchTripsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Trips
            .AsNoTracking()
            .Include(t => t.Route)
            .Include(t => t.Boat)
            .Include(t => t.TripStopCalls)
                .ThenInclude(c => c.RouteStop)
                    .ThenInclude(rs => rs!.Station)
            .Where(t => !t.IsDeleted && t.Status == TripStatus.Scheduled);

        if (request.DepartureDate.HasValue)
        {
            // Ngày khách chọn là ngày giờ Việt Nam (UTC+7); DepartureTime lưu UTC nên so theo khoảng [00:00, 24:00) giờ VN
            var date = request.DepartureDate.Value.Date;
            var dayStart = new DateTimeOffset(date, VietnamOffset);
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(t => t.DepartureTime >= dayStart && t.DepartureTime < dayEnd);
        }

        if (request.TripType.HasValue)
        {
            query = query.Where(t => t.TripType == request.TripType.Value);
        }

        var trips = await query.OrderBy(t => t.DepartureTime).ToListAsync(cancellationToken);

        // H05: bến lên/bến xuống hợp lệ của một Trip là các điểm dừng THỰC TẾ của chuyến (TripStopCall →
        // RouteStop), không suy diễn từ Route.DepartureStationId/ArrivalStationId + Station.OrderIndex
        // toàn hệ thống như trước - cách cũ bỏ sót chuyến có khách lên ở bến giữa. Cùng cơ chế resolve với
        // BookingCreationService.ResolveJourneyCalls để kết quả search luôn đặt được vé.
        var matched = new List<(Trip Trip, List<TripStopCall> Calls, TripStopCall Boarding, TripStopCall Disembarking)>();
        foreach (var trip in trips)
        {
            var calls = trip.TripStopCalls
                .Where(c => c.RouteStop != null && c.Status != "Skipped")
                .OrderBy(c => c.VisitOrder)
                .ToList();
            if (calls.Count < 2)
            {
                continue;
            }

            var boardingCall = request.DepartureStationId.HasValue
                ? calls.FirstOrDefault(c => c.RouteStop!.StationId == request.DepartureStationId.Value && c.RouteStop.CanBoard)
                : calls[0];
            if (boardingCall == null)
            {
                continue;
            }

            var disembarkingCall = request.ArrivalStationId.HasValue
                ? calls.LastOrDefault(c => c.RouteStop!.StationId == request.ArrivalStationId.Value && c.RouteStop.CanAlight)
                : calls[^1];
            if (disembarkingCall == null || boardingCall.VisitOrder >= disembarkingCall.VisitOrder)
            {
                continue;
            }

            matched.Add((trip, calls, boardingCall, disembarkingCall));
        }

        if (matched.Count == 0)
        {
            return [];
        }

        // H05: đếm ghế bận theo đúng chặng tìm kiếm bằng Segment Overlap Algorithm - cùng công thức đang
        // dùng ở SeatAvailabilityService, thay vì đếm ghế bận trên toàn bộ Trip bất kể đoạn nào.
        var tripIds = matched.Select(m => m.Trip.Id).ToList();
        var reservations = await _context.SeatReservations
            .AsNoTracking()
            .Where(r => tripIds.Contains(r.TripId) && r.Status != ReservationStatus.Cancelled)
            .Select(r => new { r.TripId, r.SeatId, r.BoardingStopOrder, r.DisembarkingStopOrder })
            .ToListAsync(cancellationToken);
        var reservationsByTrip = reservations.ToLookup(r => r.TripId);

        // BR-FARE-01/02: giá chỉ phụ thuộc TripType và SeatClass. Thẻ chuyến hiển thị giá thấp nhất ("từ ...")
        // trong các bảng giá đang hiệu lực; giá từng hạng ghế nằm ở sơ đồ ghế.
        var now = _dateTime.Now;
        var fromPriceByType = (await _context.FareRules
                .AsNoTracking()
                .Where(f => f.IsActive && f.EffectiveFrom <= now && (f.EffectiveTo == null || f.EffectiveTo > now))
                .Select(f => new { f.TripType, f.Price })
                .ToListAsync(cancellationToken))
            .GroupBy(f => f.TripType)
            .ToDictionary(g => g.Key, g => g.Min(f => f.Price));

        return matched.Select(m =>
        {
            var trip = m.Trip;
            var totalBoatSeats = trip.Boat?.TotalSeats ?? 60;

            var bookedSeatsCount = reservationsByTrip[trip.Id]
                .Where(r => m.Boarding.VisitOrder < r.DisembarkingStopOrder && m.Disembarking.VisitOrder > r.BoardingStopOrder)
                .Select(r => r.SeatId)
                .Distinct()
                .Count();

            var availableSeats = Math.Max(0, totalBoatSeats - bookedSeatsCount);

            var stops = m.Calls
                .Select(c => new TripStopDto(c.RouteStop!.StationId, c.RouteStop.Station?.Name ?? "", c.RouteStop.Station?.OrderIndex ?? c.VisitOrder))
                .ToList();

            return new TripDto(
                trip.Id,
                trip.Route?.Name ?? "Tuyến Buýt Đường Sông",
                m.Boarding.RouteStop!.Station?.Name ?? "",
                m.Disembarking.RouteStop!.Station?.Name ?? "",
                trip.DepartureTime,
                trip.ArrivalTime,
                trip.Route?.EstimatedDurationMinutes ?? 45,
                trip.TripType.ToString(),
                fromPriceByType.GetValueOrDefault(trip.TripType.ToString(), 0m),
                availableSeats,
                stops);
        }).ToList();
    }
}
