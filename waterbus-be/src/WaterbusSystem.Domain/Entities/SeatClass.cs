using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Hạng ghế (có thể cấu hình runtime, không phải enum cứng)
/// </summary>
public class SeatClass : BaseEntity
{
    public string Code { get; set; } = string.Empty;         // "SC01", "SC02", "SC03"
    public string Name { get; set; } = string.Empty;         // "Khoang trước VIP", "Tiêu chuẩn", "Boong ngoài"
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public ICollection<FareRule> FareRules { get; set; } = new List<FareRule>();
}
