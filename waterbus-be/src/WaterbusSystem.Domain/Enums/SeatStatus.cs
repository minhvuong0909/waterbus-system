namespace WaterbusSystem.Domain.Enums;

/// <summary>
/// Trạng thái của ghế trên một chuyến tàu cụ thể
/// </summary>
public enum SeatStatus
{
    /// <summary>
    /// Ghế trống - hành khách có thể chọn (Hiển thị màu Xanh Dương trên Web)
    /// </summary>
    Available = 1,

    /// <summary>
    /// Ghế đang được giữ tạm thời 10 phút trên Redis (Hiển thị màu Vàng nhấp nháy trên Web)
    /// </summary>
    Held = 2,

    /// <summary>
    /// Ghế đã thanh toán và bán thành công (Hiển thị màu Đỏ trên Web)
    /// </summary>
    Sold = 3,

    /// <summary>
    /// Ghế bị khóa bởi quản trị viên hoặc bảo trì
    /// </summary>
    Blocked = 4
}
