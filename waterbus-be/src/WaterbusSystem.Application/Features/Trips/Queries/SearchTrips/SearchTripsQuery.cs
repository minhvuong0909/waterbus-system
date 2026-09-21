using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Trips.Queries.SearchTrips;

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
    int AvailableSeatsCount);

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
            .Include(t => t.Tickets)
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

        return trips.Select(t =>
        {
            var totalBoatSeats = t.Boat?.TotalSeats ?? 60;
            var bookedSeatsCount = t.Tickets.Count(tk => 
                tk.Status == TicketStatus.Valid || 
                tk.Status == TicketStatus.CheckedIn || 
                tk.Status == TicketStatus.Pending);

            var availableSeats = Math.Max(0, totalBoatSeats - bookedSeatsCount);

            return new TripDto(
                t.Id,
                t.Route?.Name ?? "Tuyến Buýt Đường Sông",
                t.Route?.DepartureStation?.Name ?? "",
                t.Route?.ArrivalStation?.Name ?? "",
                t.DepartureTime,
                t.ArrivalTime,
                t.Route?.EstimatedDurationMinutes ?? 45,
                t.TripType.ToString(),
                t.BasePrice,
                availableSeats);
        }).ToList();
    }
}
