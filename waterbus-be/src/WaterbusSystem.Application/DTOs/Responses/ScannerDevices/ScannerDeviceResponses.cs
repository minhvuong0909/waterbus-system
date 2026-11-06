namespace WaterbusSystem.Application.DTOs.Responses.ScannerDevices;

/// <summary><paramref name="ActiveAssignmentCount"/>: phân công chưa kết thúc (ActiveUntil trống hoặc ở tương lai); <paramref name="CheckInEventCount"/>: lịch sử đã quét (chỉ để tham khảo, không bị xoá).</summary>
public record ScannerDeviceResponse(
    Guid Id, string DeviceCode, string? Label, bool IsActive, DateTimeOffset? LastPreloadedAt, int ActiveAssignmentCount, int CheckInEventCount);
