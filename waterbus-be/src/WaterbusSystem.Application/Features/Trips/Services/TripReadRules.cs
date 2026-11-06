using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Trips.Services;

/// <summary>Nguồn hành trình là TripStopCall, không suy từ Station.OrderIndex.</summary>
internal static class TripReadRules
{
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

    /// <summary>
    /// Lý do ghế không bán được (null = bán được). Dùng chung cho Search, Seat Map và Checkout để số ghế trống
    /// ở Search luôn khớp với ghế Seat Map thật sự bán được.
    /// </summary>
    internal static string? SeatBlockReason(Seat seat, FareRule? fare) =>
        !seat.IsActive || seat.SeatClass is not { IsActive: true, IsDeleted: false } ? "seatInactive"
        : fare is null ? "missingFare"
        : fare.Currency != "VND" || fare.Price < 0 ? "unsupportedCurrency" : null;

    /// <summary>Giá hiệu lực theo hạng ghế; hạng có nhiều bảng giá cùng hiệu lực bị loại (Checkout cũng từ chối).</summary>
    internal static Dictionary<Guid, FareRule> UnambiguousFares(IEnumerable<FareRule> fares) =>
        fares.GroupBy(f => f.SeatClassId).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
}
