using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Trips.Queries.GetTripDetail;

public record TripBoatDto(Guid BoatId, string Code, string Name);
public record TripCallDto(Guid CallId, Guid StationId, string StationName, int VisitOrder,
    bool CanBoard, bool CanAlight, string Status, DateTimeOffset? EstimatedArrivalTime,
    DateTimeOffset? EstimatedDepartureTime, DateTimeOffset? CheckInOpenAt, DateTimeOffset? CheckInCloseAt);
public record TripDetailDto(Guid TripId, Guid RouteId, string RouteName, string TripType, string Status,
    DateTimeOffset DepartureTime, DateTimeOffset ArrivalTime, DateTimeOffset? SalesCloseAt,
    bool IsDelayed, bool CanBook, string SalesState, TripBoatDto? Boat, IReadOnlyList<TripCallDto> Stops);
public record GetTripDetailQuery(Guid TripId) : IRequest<TripDetailDto>;

public sealed class GetTripDetailQueryValidator : AbstractValidator<GetTripDetailQuery>
{
    public GetTripDetailQueryValidator() => RuleFor(x => x.TripId).NotEmpty();
}

public sealed class GetTripDetailQueryHandler(IApplicationDbContext context, IDateTimeService clock)
    : IRequestHandler<GetTripDetailQuery, TripDetailDto>
{
    public async Task<TripDetailDto> Handle(GetTripDetailQuery request, CancellationToken ct)
    {
        var trip = await TripReadRules.LoadAsync(context, request.TripId, ct);
        var salesState = TripReadRules.SalesState(trip, clock.Now);
        return new(trip.Id, trip.RouteId, trip.Route?.Name ?? "", trip.TripType.ToString(),
            trip.Status.ToString(), trip.DepartureTime, trip.ArrivalTime, trip.SalesCloseAt,
            trip.IsDelayed, salesState == "open", salesState,
            trip.Boat is null ? null : new(trip.Boat.Id, trip.Boat.Code, trip.Boat.Name),
            trip.TripStopCalls.OrderBy(c => c.VisitOrder).Select(c => new TripCallDto(
                c.Id, c.RouteStop!.StationId, c.RouteStop.Station!.Name, c.VisitOrder,
                c.RouteStop.CanBoard && c.Status != "Skipped", c.RouteStop.CanAlight && c.Status != "Skipped",
                c.Status, c.EstimatedArrivalTime, c.EstimatedDepartureTime, c.CheckInOpenAt, c.CheckInCloseAt)).ToList());
    }
}

/// <summary>Nguồn hành trình là TripStopCall, không suy từ Station.OrderIndex.</summary>
internal static class TripReadRules
{
    internal static async Task<Trip> LoadAsync(IApplicationDbContext context, Guid tripId, CancellationToken ct)
    {
        var trip = await context.Trips.AsNoTracking().Include(t => t.Route).Include(t => t.Boat)
            .Include(t => t.TripStopCalls).ThenInclude(c => c.RouteStop).ThenInclude(s => s!.Station)
            .FirstOrDefaultAsync(t => t.Id == tripId, ct) ?? throw new NotFoundException(nameof(Trip), tripId);
        // Không xuất điểm dừng bị xóa mềm hay mất dữ liệu danh mục.
        trip.TripStopCalls = trip.TripStopCalls.Where(c => !c.IsDeleted && c.RouteStop is { IsDeleted: false }
            && c.RouteStop.Station is { IsDeleted: false }).ToList();
        return trip;
    }

    internal static string SalesState(Trip trip, DateTimeOffset now)
    {
        if (trip.Status is not (TripStatus.Scheduled or TripStatus.Boarding) || trip.ActualDepartureTime != null)
            return "tripUnavailable";
        if (trip.SalesCloseAt <= now) return "salesClosed";
        if (trip.Boat is not { IsActive: true, IsDeleted: false }) return "boatUnavailable";
        if (trip.Route is null || trip.TripStopCalls.Count < 2) return "journeyUnavailable";
        var service = trip.TripType == TripType.Sightseeing ? "Sightseeing" : "Regular";
        if (!string.Equals(trip.Route.ServiceType, service, StringComparison.OrdinalIgnoreCase)) return "journeyUnavailable";
        return "open";
    }
}
