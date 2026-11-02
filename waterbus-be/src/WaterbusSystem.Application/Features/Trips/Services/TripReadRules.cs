using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Trips.Services;

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
