using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Responses.Captains;

public record CaptainTripAttendanceResponse(Guid TripId, Guid? BoatId, Guid? CaptainId, Guid? AssignmentVersion,
    DateTimeOffset? RequiredAt, string State, CaptainAttendanceResponse? Attendance);
