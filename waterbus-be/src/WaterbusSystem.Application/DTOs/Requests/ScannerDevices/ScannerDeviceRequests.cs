namespace WaterbusSystem.Application.DTOs.Requests.ScannerDevices;

/// <summary>DeviceCode duy nhất (không phân biệt hoa thường), chỉ gồm chữ, số, '.', '_', '-'. Không đổi được sau khi đăng ký.</summary>
public record RegisterScannerDeviceRequest(string DeviceCode, string? Label);

public record UpdateScannerDeviceRequest(string? Label);

public record GetScannerDevicesRequest(int PageNumber = 1, int PageSize = 10, bool? IsActive = null, string? Search = null);
