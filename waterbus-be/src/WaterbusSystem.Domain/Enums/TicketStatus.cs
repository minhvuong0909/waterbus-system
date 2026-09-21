namespace WaterbusSystem.Domain.Enums;

/// <summary>
/// Trạng thái của vé tàu
/// </summary>
public enum TicketStatus
{
    /// <summary>
    /// Chờ thanh toán (đang giữ ghế)
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Vé hợp lệ, đã thanh toán thành công, sẵn sàng lên tàu
    /// </summary>
    Valid = 1,

    /// <summary>
    /// Vé đã được nhân viên soát vé quét mã QR tại bến (hoặc soát offline)
    /// </summary>
    CheckedIn = 2,

    /// <summary>
    /// Vé bị hủy do chuyến đi bị dừng hoặc khách hủy
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// Vé quá hạn giờ chạy tàu
    /// </summary>
    Expired = 4,

    /// <summary>
    /// Vé đã được hoàn tiền tự động 100% về tài khoản
    /// </summary>
    Refunded = 5
}
