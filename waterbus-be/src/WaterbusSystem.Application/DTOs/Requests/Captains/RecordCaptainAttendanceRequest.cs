using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Requests.Captains;

public record RecordCaptainAttendanceRequest(Guid CaptainId, Guid AssignmentVersion, string Reason);
