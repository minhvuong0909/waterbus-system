using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Thực thể Ghế cố định gắn liền với từng Tàu
/// </summary>
public class Seat : BaseEntity
{
    public Guid BoatId { get; set; }
    public Boat? Boat { get; set; }

    /// <summary>
    /// Mã ghế trên sơ đồ (Ví dụ: F01, S15, O08)
    /// </summary>
    public string SeatCode { get; set; } = string.Empty;

    /// <summary>
    /// Phân loại khoang ghế (Khoang trước VIP, Tiêu chuẩn, Boong ngoài trời)
    /// </summary>
    public SeatCategory Category { get; set; } = SeatCategory.Standard;

    /// <summary>
    /// Số hàng ghế (phục vụ render grid trên giao diện web/mobile)
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// Số cột ghế
    /// </summary>
    public int ColumnNumber { get; set; }

    /// <summary>
    /// Hệ số nhân giá vé theo vị trí ghế (Ví dụ VIP: 1.2, Tiêu chuẩn: 1.0, Ngoài trời: 1.1)
    /// </summary>
    public decimal PriceMultiplier { get; set; } = 1.0m;

    public bool IsActive { get; set; } = true;
}
