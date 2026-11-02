namespace WaterbusSystem.Application.DTOs.Responses.CheckIns;

public record AssignScannerResponse(
    Guid AssignmentId,
    Guid DeviceId,
    Guid StaffAccountId,
    Guid TripStopCallId,
    bool IsPrimaryOffline,
    DateTimeOffset ActiveFrom,
    DateTimeOffset? ActiveUntil);
