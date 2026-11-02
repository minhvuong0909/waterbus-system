using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Requests.Trips;

public record SearchTripsRequest(
    Guid? DepartureStationId,
    Guid? ArrivalStationId,
    DateTime? DepartureDate,
    TripType? TripType);
