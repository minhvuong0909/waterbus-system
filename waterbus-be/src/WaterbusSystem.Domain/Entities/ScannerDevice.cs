using WaterbusSystem.Domain.Common;
namespace WaterbusSystem.Domain.Entities;
/// <summary>Thiết bị quét QR tại bến (online hoặc offline)</summary>
public class ScannerDevice : BaseEntity
{
    public string DeviceCode { get; set; } = string.Empty;  // UNIQUE
    public string? Label { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastPreloadedAt { get; set; }
}
