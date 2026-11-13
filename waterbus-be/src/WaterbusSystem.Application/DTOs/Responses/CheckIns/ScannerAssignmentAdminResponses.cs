namespace WaterbusSystem.Application.DTOs.Responses.CheckIns;

public record ScannerStaffResponse(Guid Id, string FullName, string? StaffCode, string? Email);

public record ScannerAssignmentAdminResponse(
    Guid AssignmentId, Guid StaffAccountId, string? StaffName, string DeviceCode,
    bool IsPrimaryOffline, DateTimeOffset ActiveFrom, DateTimeOffset? ActiveUntil);
