using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Responses.Captains;

public record CaptainAttendanceResponse(Guid Id, Guid TripId, Guid BoatId, Guid CaptainId, Guid AssignmentVersion,
    Guid RecordedByAdminId, DateTimeOffset RecordedAt, DateTimeOffset RequiredAt, bool IsLate, string Reason);
