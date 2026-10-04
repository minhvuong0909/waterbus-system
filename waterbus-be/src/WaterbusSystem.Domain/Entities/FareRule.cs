using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Bảng giá vé theo TripType × SeatClass.
/// Giá KHÔNG phụ thuộc Route, khoảng cách, giờ chạy hay ghế cụ thể.
/// EffectiveFrom/To chỉ dùng để quản lý phiên bản bảng giá, KHÔNG phải giá động theo thời gian.
/// </summary>
public class FareRule : BaseEntity
{
    public string TripType { get; set; } = string.Empty;         // "Commuter" hoặc "Sightseeing"
    public Guid SeatClassId { get; set; }
    public SeatClass? SeatClass { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByAdminId { get; set; }                  // FK → ApplicationUser (optional)
}
