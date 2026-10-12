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
    /// Tham chiếu đến hạng ghế (thay thế cho SeatCategory enum cũ)
    /// </summary>
    public Guid SeatClassId { get; set; }
    public SeatClass? SeatClass { get; set; }

    /// <summary>
    /// Số hàng ghế (phục vụ render grid trên giao diện web/mobile)
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// Số cột ghế
    /// </summary>
    public int ColumnNumber { get; set; }

    public bool IsActive { get; set; } = true;
}
