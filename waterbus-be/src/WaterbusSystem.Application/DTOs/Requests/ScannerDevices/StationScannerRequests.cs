using System.ComponentModel.DataAnnotations;

namespace WaterbusSystem.Application.DTOs.Requests.ScannerDevices;

public record CreateStationScannerRequest(Guid StationId, [StringLength(100)] string? Label);
public record TransferScannerRequest(Guid StationId);
public record RedeemScannerInvitationRequest([Required, StringLength(128)] string Activation);
public record CreateStaffShiftRequest(Guid StaffAccountId, Guid StationId, DateTimeOffset ActiveFrom, DateTimeOffset ActiveUntil);
public record ResolveScannerReviewRequest(bool Approve, [Required, StringLength(500)] string Reason);
public record CreatePrimaryScannerRequest(Guid DeviceId, Guid TripStopCallId, DateTimeOffset ActiveFrom, DateTimeOffset ActiveUntil);
