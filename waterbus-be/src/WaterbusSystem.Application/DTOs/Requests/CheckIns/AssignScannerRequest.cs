namespace WaterbusSystem.Application.DTOs.Requests.CheckIns;

public record AssignScannerRequest(
    Guid DeviceId,
    Guid StaffAccountId,
    Guid TripStopCallId,
    bool IsPrimaryOffline,
    DateTimeOffset ActiveFrom,
    DateTimeOffset? ActiveUntil);
