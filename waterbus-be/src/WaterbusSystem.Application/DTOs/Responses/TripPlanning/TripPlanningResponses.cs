namespace WaterbusSystem.Application.DTOs.Responses.TripPlanning;

public record GeneratedTripResponse(Guid TripId, DateTimeOffset DepartureTime, DateTimeOffset ArrivalTime);

public record GenerateTripsResponse(
    Guid ScheduleId, List<GeneratedTripResponse> Created, int SkippedExisting, int SkippedPast, int SkippedNotOperatingDay);

public record AssignTripBoatResponse(Guid TripId, Guid BoatId, Guid? PreviousBoatId, int RemappedReservations);

public record CreateAdHocTripResponse(Guid TripId, DateTimeOffset DepartureTime, DateTimeOffset ArrivalTime, int StopCount);
