namespace WaterbusSystem.Application.Common.Models;

/// <summary>Chuyến đang cần theo dõi và tuyến của nó.</summary>
public record ActiveTripRef(Guid TripId, Guid RouteId);
