namespace WaterbusSystem.Domain.Enums;

/// <summary>
/// Trạng thái đơn đặt chỗ (Booking)
/// </summary>
public enum BookingStatus
{
    Draft = 0,
    PendingPayment = 1,
    Pending = PendingPayment, // Compatibility with the existing booking API/database.
    Confirmed = 2,  // Đã thanh toán thành công và xác nhận vé
    Cancelled = 3,  // Hết 10 phút không thanh toán hoặc bị hủy do thời tiết
    Refunded = 4,   // Legacy value; refund progress belongs to FinancialTransaction.
    Terminated = 5,
    Completed = 6
}
