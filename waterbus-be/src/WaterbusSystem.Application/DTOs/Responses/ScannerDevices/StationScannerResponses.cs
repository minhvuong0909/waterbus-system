namespace WaterbusSystem.Application.DTOs.Responses.ScannerDevices;

public record ScannerIdentityResponse(Guid DeviceId, Guid EnrollmentId, string DeviceCode, Guid StationId, string StationName);
public record ScannerPairingResponse(ScannerIdentityResponse Scanner, string Credential);
public record ScannerInvitationResponse(string Activation, DateTimeOffset ExpiresAt);
public record StaffStationShiftResponse(Guid Id, Guid StaffAccountId, string StaffName, Guid StationId, string StationName,
    DateTimeOffset ActiveFrom, DateTimeOffset ActiveUntil, DateTimeOffset? RevokedAt);
public record ScannerOfflineGrantResponse(Guid Id, DateTimeOffset ActiveFrom, DateTimeOffset ActiveUntil);
public record ScannerAuditResponse(Guid Id, Guid TripStopCallId, Guid StaffAccountId, Guid TicketId,
    string Outcome, DateTimeOffset OccurredAtDevice, DateTimeOffset? ReceivedAtServer, DateTimeOffset? ReviewedAt, string? ReviewNote);
