namespace WaterbusSystem.Application.DTOs.Responses.ScannerDevices;

public record ScannerDeviceResponse(
    Guid Id, string DeviceCode, string? Label, bool IsActive, DateTimeOffset? LastPreloadedAt,
    int ActiveAssignmentCount, int CheckInEventCount, Guid? StationId = null, string? StationName = null,
    string Status = "Unbound", DateTimeOffset? InvitationExpiresAt = null);
